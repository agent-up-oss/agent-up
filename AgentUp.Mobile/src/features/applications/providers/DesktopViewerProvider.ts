import { requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';

export type DesktopViewerTicket = {
  viewerUrl: string;
  expiresAtUtc: string;
};

export async function createDesktopViewerUrl(
  server: ServerSession,
  workspaceId: string,
  application: string,
  request: typeof fetch = fetch,
): Promise<string> {
  const path = `/api/desktop-applications/${encodeURIComponent(workspaceId)}/${encodeURIComponent(application)}/viewer-ticket`;
  const ticket = await requestServerJson<DesktopViewerTicket>(server, path, { method: 'POST' }, undefined, request);
  if (!ticket?.viewerUrl) throw new Error('The Server did not return a desktop viewer URL.');
  return new URL(ticket.viewerUrl, `${server.url}/`).toString();
}
