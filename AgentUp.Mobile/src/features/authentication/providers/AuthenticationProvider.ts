import { ensureCredentialTransportAllowed } from '@/features/servers/providers/ServerRequestProvider';

export type AuthenticationStatus = { authenticationRequired: boolean };
export type LoginResult = AuthenticationStatus & { accessToken?: string };

export { ensureCredentialTransportAllowed };

export async function getAuthenticationStatus(url: string, request: typeof fetch = fetch): Promise<AuthenticationStatus> {
  const response = await request(`${url}/api/auth/status`, { headers: { Accept: 'application/json' } });
  if (!response.ok) throw new Error(`Server returned ${response.status}.`);
  return response.json() as Promise<AuthenticationStatus>;
}

export async function login(url: string, password: string, request: typeof fetch = fetch): Promise<LoginResult> {
  ensureCredentialTransportAllowed(url);
  const response = await request(`${url}/api/auth/login`, {
    method: 'POST',
    redirect: 'error',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ password }),
  });
  if (response.status === 401) throw new Error('The admin password is incorrect.');
  if (!response.ok) throw new Error(`Server returned ${response.status}.`);
  return response.json() as Promise<LoginResult>;
}
