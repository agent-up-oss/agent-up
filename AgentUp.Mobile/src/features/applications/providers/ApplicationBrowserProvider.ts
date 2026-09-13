import { ensureCredentialTransportAllowed, requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { WorkspaceApplication } from '@/features/workspaces/models/Workspace';

/** Returns the Server-local URL for an application's first HTTP port. */
export function applicationHttpUrl(application: WorkspaceApplication): string | null {
  const port = application.allocatedPorts?.find(entry => entry.protocol.toLowerCase() === 'http');
  return port ? `http://127.0.0.1:${port.allocatedPort}/` : null;
}

/** Builds a viewer URL without placing the bearer credential in the HTTP request target. */
export function browserViewerUrl(server: ServerSession, workspaceId: string): string {
  validateCredentialTransport(server);
  const query = `workspaceId=${encodeURIComponent(workspaceId)}`;
  const credential = server.accessToken
    ? `#access_token=${encodeURIComponent(server.accessToken)}`
    : '';
  return `${server.url}/api/browser/rdp-viewer?${query}${credential}`;
}

/** Navigates the workspace browser to an application unless the caller supersedes the request. */
export async function navigateApplicationBrowser(
  server: ServerSession,
  workspaceId: string,
  application: WorkspaceApplication,
  request: typeof fetch = fetch,
  signal?: AbortSignal,
): Promise<void> {
  validateCredentialTransport(server);
  const url = applicationHttpUrl(application);
  if (!url) throw new Error(`${application.name} does not expose an HTTP port.`);
  await requestServerJson(
    server,
    `/api/browser/navigate/${encodeURIComponent(workspaceId)}?url=${encodeURIComponent(url)}&reloadIfSameUrl=false`,
    { method: 'POST', signal },
    undefined,
    request,
  );
}

function validateCredentialTransport(server: ServerSession): void {
  if (server.accessToken) ensureCredentialTransportAllowed(server.url);
}
