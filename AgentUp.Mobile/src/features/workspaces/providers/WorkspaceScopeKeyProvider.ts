export function workspaceScopeKey(connectionId: string, workspaceId: string): string {
  return `${connectionId}:${workspaceId}`;
}
