import type { Workspace } from '../models/Workspace';
import { isDerivedWorkspace } from './WorkspaceLabelProvider';

export function workspaceActivePriority(state: string): number {
  if (state === 'Running') return 2;
  if (state === 'Starting') return 1;
  return 0;
}

export function ownWorkspacePriority(derived: boolean): number {
  return derived ? 0 : 1;
}

export function orderWorkspaces(workspaces: Workspace[]): Workspace[] {
  return workspaces.slice().sort((left, right) => {
    const own = ownWorkspacePriority(isDerivedWorkspace(right)) - ownWorkspacePriority(isDerivedWorkspace(left));
    if (own !== 0) return own;
    const active = workspaceActivePriority(right.state) - workspaceActivePriority(left.state);
    if (active !== 0) return active;
    return left.displayName.localeCompare(right.displayName);
  });
}
