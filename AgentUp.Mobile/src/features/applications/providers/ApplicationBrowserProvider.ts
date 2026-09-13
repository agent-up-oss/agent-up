import { ensureCredentialTransportAllowed, jsonBody, requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { WorkspaceApplication } from '@/features/workspaces/models/Workspace';

export type ApplicationProxyTicket = {
  ticket: string;
  bootstrapPath: string;
  expiresAt: string;
};

/** Returns the first allocated HTTP port that the Server can tunnel. */
export function applicationHttpPort(application: WorkspaceApplication): number | null {
  const port = application.allocatedPorts?.find(entry => entry.protocol.toLowerCase() === 'http');
  return port ? port.allocatedPort : null;
}

/** Builds the one-time HTTPS bootstrap URL for the native WebView. */
export function applicationProxyUrl(server: ServerSession, ticket: ApplicationProxyTicket): string {
  validateCredentialTransport(server);
  const separator = ticket.bootstrapPath.includes('?') ? '&' : '?';
  return `${server.url}${ticket.bootstrapPath}${separator}ticket=${encodeURIComponent(ticket.ticket)}`;
}

/** Asks the Server for a single-use ticket that opens one allocated HTTP port. */
export async function issueApplicationProxyTicket(
  server: ServerSession,
  workspaceId: string,
  application: WorkspaceApplication,
  request: typeof fetch = fetch,
  signal?: AbortSignal,
): Promise<ApplicationProxyTicket> {
  validateCredentialTransport(server);
  const allocatedPort = applicationHttpPort(application);
  if (allocatedPort === null) throw new Error(`${application.name} does not expose an HTTP port.`);
  const ticket = await requestServerJson<ApplicationProxyTicket>(
    server,
    '/api/apps/tickets',
    { ...jsonBody({ workspaceId, allocatedPort }), signal },
    undefined,
    request,
  );
  if (!ticket?.ticket || !ticket.bootstrapPath) throw new Error('The Server did not issue an application proxy ticket.');
  return ticket;
}

function validateCredentialTransport(server: ServerSession): void {
  if (server.accessToken) ensureCredentialTransportAllowed(server.url);
}
