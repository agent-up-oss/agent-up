import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { Workspace } from '../models/Workspace';

// Where a refresh reports its outcome. The controller supplies React state setters; keeping the
// sequencing here lets the "latest response wins" rule be tested without a renderer.
export type WorkspaceRefreshSink = {
  onLoading(loading: boolean): void;
  onWorkspaces(workspaces: Workspace[]): void;
  onError(message: string): void;
  onDisconnected(): void;
};

export type WorkspaceRefresher = {
  // Loads the workspaces of one server. Any reply belonging to an earlier call is ignored, so a
  // slow response for the server the user switched away from cannot replace the current one.
  refresh(server: ServerSession | null): Promise<void>;
  // Whether this server is the one the most recent refresh targeted. Long-running work started on a
  // server must check this before touching state, since finishing does not make that server current.
  // Servers are compared by URL, so re-authenticating against the same server stays active.
  isActive(server: ServerSession | null): boolean;
};

export function createWorkspaceRefresh(
  sink: WorkspaceRefreshSink,
  list: (server: ServerSession) => Promise<Workspace[]>,
): WorkspaceRefresher {
  let generation = 0;
  let activeServerUrl: string | null = null;

  return {
    isActive: server => (server?.url ?? null) === activeServerUrl,

    async refresh(server: ServerSession | null): Promise<void> {
      activeServerUrl = server?.url ?? null;
      const ticket = ++generation;
      if (!server) {
        sink.onDisconnected();
        sink.onLoading(false);
        return;
      }

      sink.onLoading(true);
      try {
        const loaded = await list(server);
        if (ticket !== generation) return;
        sink.onWorkspaces(loaded);
      } catch (cause) {
        if (ticket !== generation) return;
        sink.onError(cause instanceof Error ? cause.message : 'Could not load workspaces.');
      } finally {
        if (ticket === generation) sink.onLoading(false);
      }
    },
  };
}
