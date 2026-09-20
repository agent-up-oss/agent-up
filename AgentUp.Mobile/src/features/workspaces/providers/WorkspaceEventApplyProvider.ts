import type { Workspace, WorkspaceApplication } from '../models/Workspace';

export type WorkspaceAppStateChange = {
  name: string;
  state: string;
};

export type WorkspaceStateChangedEvent = {
  workspaceId: string;
  state: string;
  healthState?: string | null;
  applications: WorkspaceAppStateChange[];
};

export type AppliedWorkspaceEvent = {
  applied: boolean;
  workspaces: Workspace[];
};

// Mirrors Desktop's in-place SSE apply: keep the existing workspace objects' identity fields,
// update only Server-owned lifecycle/health, and do not drop applications the event omitted.
export function applyWorkspaceEvent(
  workspaces: Workspace[],
  event: WorkspaceStateChangedEvent,
): AppliedWorkspaceEvent {
  if (event.state === 'Removed') {
    return {
      applied: true,
      workspaces: workspaces.filter(workspace => workspace.id !== event.workspaceId),
    };
  }

  const index = workspaces.findIndex(workspace => workspace.id === event.workspaceId);
  if (index < 0) return { applied: false, workspaces };

  const current = workspaces[index];
  const next: Workspace = {
    ...current,
    state: event.state,
    healthState: event.healthState ?? undefined,
    applications: mergeApplications(current.applications ?? [], event.applications),
  };

  const updated = workspaces.slice();
  updated[index] = next;
  return { applied: true, workspaces: updated };
}

function mergeApplications(
  current: WorkspaceApplication[],
  changes: WorkspaceAppStateChange[],
): WorkspaceApplication[] {
  const byName = new Map(current.map(application => [application.name, application]));
  for (const change of changes) {
    const existing = byName.get(change.name);
    byName.set(change.name, existing ? { ...existing, state: change.state } : { name: change.name, state: change.state });
  }
  return [...byName.values()];
}
