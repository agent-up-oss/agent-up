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
  refresh(serverUrl: string | null): Promise<void>;
  // Whether serverUrl is the one the most recent refresh targeted. Long-running work started on a
  // server must check this before touching state, since finishing does not make that server current.
  isActive(serverUrl: string | null): boolean;
};

export function createWorkspaceRefresh(
  sink: WorkspaceRefreshSink,
  list: (serverUrl: string) => Promise<Workspace[]>,
): WorkspaceRefresher {
  let generation = 0;
  let activeServerUrl: string | null = null;

  return {
    isActive: serverUrl => serverUrl === activeServerUrl,

    async refresh(serverUrl: string | null): Promise<void> {
      activeServerUrl = serverUrl;
      const ticket = ++generation;
      if (!serverUrl) {
        sink.onDisconnected();
        sink.onLoading(false);
        return;
      }

      sink.onLoading(true);
      try {
        const loaded = await list(serverUrl);
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
