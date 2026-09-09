import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { CloneSourceRequest, Workspace } from '../models/Workspace';
import type { WorkspaceRefresher } from './WorkspaceRefreshProvider';

export type WorkspaceActionsApi = {
  clone(server: ServerSession, request: CloneSourceRequest): Promise<Workspace>;
  start(server: ServerSession, workspaceId: string): Promise<void>;
  stop(server: ServerSession, workspaceId: string): Promise<void>;
};

export type WorkspaceActionsSink = {
  onSelect(workspaceId: string): void;
};

export type WorkspaceActions = {
  clone(server: ServerSession | null, request: CloneSourceRequest): Promise<Workspace>;
  start(server: ServerSession | null, workspaceId: string): Promise<void>;
  stop(server: ServerSession | null, workspaceId: string): Promise<void>;
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
    async clone(server, request) {
      if (!server) throw new Error('Connect this client to an Agent-Up Server first.');

      const workspace = await api.clone(server, request);
      if (!refresher.isActive(server)) return workspace;

      await refresher.refresh(server);
      sink.onSelect(workspace.id);
      return workspace;
    },

    async start(server, workspaceId) {
      if (!server) return;

      await api.start(server, workspaceId);
      if (refresher.isActive(server)) await refresher.refresh(server);
    },

    async stop(server, workspaceId) {
      if (!server) return;

      await api.stop(server, workspaceId);
      if (refresher.isActive(server)) await refresher.refresh(server);
    },
  };
}
