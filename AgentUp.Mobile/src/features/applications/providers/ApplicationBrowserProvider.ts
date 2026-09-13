import { requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { WorkspaceApplication } from '@/features/workspaces/models/Workspace';

export function applicationHttpUrl(application: WorkspaceApplication): string | null {
  const port = application.allocatedPorts?.find(entry => entry.protocol.toLowerCase() === 'http');
  return port ? `http://127.0.0.1:${port.allocatedPort}/` : null;
}

export function browserViewerUrl(server: ServerSession, workspaceId: string): string {
  const query = `workspaceId=${encodeURIComponent(workspaceId)}`;
  const credential = server.accessToken
    ? `#access_token=${encodeURIComponent(server.accessToken)}`
    : '';
  return `${server.url}/api/browser/rdp-viewer?${query}${credential}`;
}

export async function navigateApplicationBrowser(
  server: ServerSession,
  workspaceId: string,
  application: WorkspaceApplication,
  request: typeof fetch = fetch,
): Promise<void> {
  const url = applicationHttpUrl(application);
  if (!url) throw new Error(`${application.name} does not expose an HTTP port.`);
  await requestServerJson(
    server,
    `/api/browser/navigate/${encodeURIComponent(workspaceId)}?url=${encodeURIComponent(url)}&reloadIfSameUrl=false`,
    { method: 'POST' },
    undefined,
    request,
  );
}
