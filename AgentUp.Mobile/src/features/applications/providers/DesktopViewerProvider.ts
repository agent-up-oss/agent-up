import { requestServerJson, ServerRequestError, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';

export type DesktopViewerTicket = {
  viewerUrl: string;
  expiresAtUtc: string;
};

export const DESKTOP_TICKET_RETRY_LIMIT = 150;
export const DESKTOP_TICKET_RETRY_DELAY_MS = 200;

export function shouldRetryDesktopTicket(state: string): boolean {
  return state === 'Starting' || state === 'Running';
}

export function isRetryableDesktopTicketStatus(status: number): boolean {
  return status === 404 || status === 503;
}

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

type WaitForDesktopViewerUrlOptions = {
  applicationState: () => string;
  request?: typeof fetch;
  delay?: (ms: number) => Promise<void>;
  maxAttempts?: number;
  signal?: AbortSignal;
};

export async function waitForDesktopViewerUrl(
  server: ServerSession,
  workspaceId: string,
  application: string,
  options: WaitForDesktopViewerUrlOptions,
): Promise<string> {
  const request = options.request ?? fetch;
  const delay = options.delay ?? defaultDelay;
  const maxAttempts = options.maxAttempts ?? DESKTOP_TICKET_RETRY_LIMIT;
  const signal = options.signal;

  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    throwIfAborted(signal);
    try {
      return await createDesktopViewerUrl(server, workspaceId, application, request);
    } catch (error) {
      const retry = shouldRetryDesktopTicket(options.applicationState())
        && isRetryableTicketError(error)
        && attempt + 1 < maxAttempts;
      if (!retry) throw error;
    }

    throwIfAborted(signal);
    await delay(DESKTOP_TICKET_RETRY_DELAY_MS);
  }

  throw new Error('The desktop application did not become available.');
}

function isRetryableTicketError(error: unknown): boolean {
  return error instanceof ServerRequestError && isRetryableDesktopTicketStatus(error.status);
}

function throwIfAborted(signal?: AbortSignal): void {
  if (!signal?.aborted) return;
  const abortError = new Error('The desktop viewer request was cancelled.');
  abortError.name = 'AbortError';
  throw abortError;
}

function defaultDelay(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}
