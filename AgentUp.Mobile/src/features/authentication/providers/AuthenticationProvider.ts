export type AuthenticationStatus = { authenticationRequired: boolean };
export type LoginResult = AuthenticationStatus & { accessToken?: string };

function isLoopbackHost(hostname: string): boolean {
  const normalized = hostname.toLowerCase();
  return normalized === 'localhost' || normalized === '127.0.0.1' || normalized === '::1';
}

export function ensureCredentialTransportAllowed(url: string): void {
  const parsed = new URL(url);
  if (parsed.protocol === 'https:') return;
  if (parsed.protocol === 'http:' && isLoopbackHost(parsed.hostname)) return;
  throw new Error('HTTPS is required for remote Agent-Up server URLs.');
}

export async function getAuthenticationStatus(url: string, request: typeof fetch = fetch): Promise<AuthenticationStatus> {
  const response = await request(`${url}/api/auth/status`, { headers: { Accept: 'application/json' } });
  if (!response.ok) throw new Error(`Server returned ${response.status}.`);
  return response.json() as Promise<AuthenticationStatus>;
}

export async function login(url: string, password: string, request: typeof fetch = fetch): Promise<LoginResult> {
  ensureCredentialTransportAllowed(url);
  const response = await request(`${url}/api/auth/login`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ password }),
  });
  if (response.status === 401) throw new Error('The admin password is incorrect.');
  if (!response.ok) throw new Error(`Server returned ${response.status}.`);
  return response.json() as Promise<LoginResult>;
}
