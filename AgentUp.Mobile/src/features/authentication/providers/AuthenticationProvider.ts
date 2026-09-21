import { ensureCredentialTransportAllowed } from '@/features/servers/providers/ServerRequestProvider';
import type { ConnectionMetadata } from '../models/Connection';

export type AuthenticationStatus = { authenticationRequired: boolean };
export type LoginResult = AuthenticationStatus & { accessToken?: string };

export { ensureCredentialTransportAllowed };

export async function getAuthenticationStatus(url: string, request: typeof fetch = fetch): Promise<AuthenticationStatus> {
  const response = await request(`${url}/api/auth/status`, { headers: { Accept: 'application/json' } });
  if (!response.ok) throw new Error(`Server returned ${response.status}.`);
  return response.json() as Promise<AuthenticationStatus>;
}

export async function getConnection(url: string, request: typeof fetch = fetch): Promise<ConnectionMetadata | null> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 8000);
  try {
    const response = await request(`${url}/api/connection`, {
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    });
    if (!response.ok) return null;
    const connection = await response.json() as Partial<ConnectionMetadata>;
    return isConnectionMetadata(connection) ? connection : null;
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError')
      throw new Error('The server did not respond in time.');
    throw error;
  } finally {
    clearTimeout(timeout);
  }
}

function isConnectionMetadata(value: Partial<ConnectionMetadata> | null | undefined): value is ConnectionMetadata {
  return !!value?.kind
    && typeof value.authentication?.mode === 'string'
    && typeof value.authentication.prompt === 'string';
}

export async function login(
  url: string,
  password: string,
  request: typeof fetch = fetch,
): Promise<LoginResult> {
  ensureCredentialTransportAllowed(url);
  const response = await request(`${url}/api/auth/login`, {
    method: 'POST',
    redirect: 'error',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ password }),
  });
  if (response.status === 401) throw new Error('The sign-in is incorrect.');
  if (!response.ok) throw new Error(`Server returned ${response.status}.`);
  return response.json() as Promise<LoginResult>;
}

