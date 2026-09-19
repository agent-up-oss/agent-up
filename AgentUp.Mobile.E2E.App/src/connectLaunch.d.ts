export function connectLaunchUrl(serverUrl: string, workspaceId: string): string;
export function parseConnectLaunchUrl(url: string | null | undefined): { url: string; workspaceId: string } | null;
