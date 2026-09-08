import type { Workspace } from '../models/Workspace';

// Where a refresh reports its outcome. The controller supplies React state setters; keeping the
// sequencing here lets the "latest response wins" rule be tested without a renderer.
export type WorkspaceRefreshSink = {
  onLoading(loading: boolean): void;
  onWorkspaces(workspaces: Workspace[]): void;
  onError(message: string): void;
  onDisconnected(): void;
};

// Returns a refresh function that ignores any reply belonging to an earlier call. Without this a
// slow response for the server the user just switched away from would replace the workspaces,
// selection, error, and loading state of the server they are now on.
export function createWorkspaceRefresh(
  sink: WorkspaceRefreshSink,
  list: (serverUrl: string) => Promise<Workspace[]>,
): (serverUrl: string | null) => Promise<void> {
  let generation = 0;

  return async function refresh(serverUrl: string | null): Promise<void> {
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
  };
}
