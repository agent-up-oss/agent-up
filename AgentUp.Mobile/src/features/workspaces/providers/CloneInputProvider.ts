// Clone dialog input rule, kept out of the screen so the boundary it guards is directly testable.
export function canCloneWorkspace(repository: string, branch: string): boolean {
  return repository.trim().length > 0 && branch.trim().length > 0;
}
