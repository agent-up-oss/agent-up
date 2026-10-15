import type { Workspace } from '../models/Workspace';

export function isDerivedWorkspace(workspace: Pick<Workspace, 'derived'>): boolean {
  return workspace.derived === true;
}

export function workspaceSubtitle(workspace: Pick<Workspace, 'derived' | 'branch' | 'sourceRevision'>): string {
  return isDerivedWorkspace(workspace) ? workspace.sourceRevision || workspace.branch : workspace.branch;
}

export function cloneRevisionLabel(derived: boolean): string {
  return derived ? 'Revision' : 'Branch';
}

export function cloneConfirmLabel(derived: boolean, cloning: boolean): string {
  if (cloning) return 'Cloning…';
  return derived ? 'Start review' : 'Clone';
}
