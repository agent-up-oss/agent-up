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

// fetch resolves as soon as response headers arrive, so the body is read inside the same timeout
// window. Clearing the timer earlier would let a server that stalls its body hang the client.
export async function requestServerJson<T>(
  server: ServerSession,
  path: string,
  init: RequestInit = {},
  timeoutMs: number = DEFAULT_TIMEOUT_MS,
  request: typeof fetch = fetch,
): Promise<T | null> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
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
    throw toReadableError(error, server.url);
  } finally {
    clearTimeout(timeout);
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
