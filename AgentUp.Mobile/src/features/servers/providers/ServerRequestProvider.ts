// Shared transport for the Agent-Up Server REST API. The servers slice owns connectivity to a
// configured server; feature slices call this instead of reimplementing fetch handling.

export const DEFAULT_TIMEOUT_MS = 15000;

// A server plus the credential the client holds for it. The Server requires a bearer token unless
// it was started with authentication disabled, so the token travels with the URL rather than being
// looked up separately by each slice.
export type ServerSession = {
  url: string;
  accessToken?: string;
};

/** Rejects credential transport over plaintext non-loopback connections. */
export function ensureCredentialTransportAllowed(url: string): void {
  const parsed = new URL(url);
  const loopback = parsed.hostname.toLowerCase() === 'localhost'
    || parsed.hostname === '127.0.0.1'
    || parsed.hostname === '::1';
  if (parsed.protocol === 'https:' || (parsed.protocol === 'http:' && loopback)) return;
  throw new Error('HTTPS is required for remote Agent-Up server URLs.');
}

// fetch resolves as soon as response headers arrive, so the body is read inside the same timeout
// window. Clearing the timer earlier would let a server that stalls its body hang the client.
/** Performs one authenticated Server JSON request with timeout and caller cancellation. */
export async function requestServerJson<T>(
  server: ServerSession,
  path: string,
  init: RequestInit = {},
  timeoutMs: number = DEFAULT_TIMEOUT_MS,
  request: typeof fetch = fetch,
): Promise<T | null> {
  if (server.accessToken) ensureCredentialTransportAllowed(server.url);
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  const abortFromCaller = () => controller.abort();
  if (init.signal?.aborted) controller.abort();
  else init.signal?.addEventListener('abort', abortFromCaller, { once: true });
  try {
    const response = await request(`${server.url}${path}`, {
      ...init,
      headers: {
        Accept: 'application/json',
        ...authorizationHeader(server.accessToken),
        ...(init.headers ?? {}),
      },
      signal: controller.signal,
    });
    if (!response.ok) throw new ServerRequestError(await readProblemDetail(response), response.status);
    return await readJsonBody<T>(response);
  } catch (error) {
    if (init.signal?.aborted && error instanceof Error && error.name === 'AbortError')
      throw new Error('The request was cancelled.');
    throw toReadableError(error, server.url);
  } finally {
    clearTimeout(timeout);
    init.signal?.removeEventListener('abort', abortFromCaller);
  }
}

function authorizationHeader(accessToken?: string): Record<string, string> {
  return accessToken ? { Authorization: `Bearer ${accessToken}` } : {};
}

export function jsonBody(value: unknown): RequestInit {
  return { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(value) };
}

export class ServerRequestError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ServerRequestError';
    this.status = status;
  }
}

async function readJsonBody<T>(response: Response): Promise<T | null> {
  if (response.status === 204) return null;
  const body = await response.text();
  if (!body) return null;
  return JSON.parse(body) as T;
}

export async function readProblemDetail(response: Response): Promise<string> {
  try {
    const body = await response.text();
    if (!body) return `The server returned ${response.status}.`;
    const parsed = JSON.parse(body) as { detail?: string; title?: string };
    return parsed.detail ?? parsed.title ?? body;
  } catch {
    return `The server returned ${response.status}.`;
  }
}

export function toReadableError(error: unknown, serverUrl: string): Error {
  if (error instanceof Error && error.name === 'AbortError')
    return new Error('The server did not respond in time.');
  if (error instanceof TypeError)
    return new Error(`Could not reach ${serverUrl}. Check that Agent-Up Server is running and reachable from this device.`);
  return error instanceof Error ? error : new Error(String(error));
}

export function isUnauthorized(error: unknown): boolean {
  return error instanceof ServerRequestError && error.status === 401;
}
