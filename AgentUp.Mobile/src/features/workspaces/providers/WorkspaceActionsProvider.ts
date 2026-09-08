import type { CloneSourceRequest, Workspace } from '../models/Workspace';
import type { WorkspaceRefresher } from './WorkspaceRefreshProvider';

export type WorkspaceActionsApi = {
  clone(serverUrl: string, request: CloneSourceRequest): Promise<Workspace>;
  start(serverUrl: string, workspaceId: string): Promise<void>;
  stop(serverUrl: string, workspaceId: string): Promise<void>;
};

export type WorkspaceActionsSink = {
  onSelect(workspaceId: string): void;
};

export type WorkspaceActions = {
  clone(serverUrl: string | null, request: CloneSourceRequest): Promise<Workspace>;
  start(serverUrl: string | null, workspaceId: string): Promise<void>;
  stop(serverUrl: string | null, workspaceId: string): Promise<void>;
};

// Actions that outlive their own request. A clone can run for minutes, so by the time it returns
// the user may be on another server; refreshing or selecting then would pull the previous server's
// workspaces back over the current one. Every action re-checks that its server is still active
// before it touches shared state.
export function createWorkspaceActions(
  refresher: WorkspaceRefresher,
  api: WorkspaceActionsApi,
  sink: WorkspaceActionsSink,
): WorkspaceActions {
  return {
    async clone(serverUrl, request) {
      if (!serverUrl) throw new Error('Connect this client to an Agent-Up Server first.');

      const workspace = await api.clone(serverUrl, request);
      if (!refresher.isActive(serverUrl)) return workspace;

      await refresher.refresh(serverUrl);
      sink.onSelect(workspace.id);
      return workspace;
    },

    async start(serverUrl, workspaceId) {
      if (!serverUrl) return;

      await api.start(serverUrl, workspaceId);
      if (refresher.isActive(serverUrl)) await refresher.refresh(serverUrl);
    },

    async stop(serverUrl, workspaceId) {
      if (!serverUrl) return;

      await api.stop(serverUrl, workspaceId);
      if (refresher.isActive(serverUrl)) await refresher.refresh(serverUrl);
    },
  };
}
