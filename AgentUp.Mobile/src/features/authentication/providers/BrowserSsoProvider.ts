const pendingStartKey = 'agent-up.sso.pending';

export type PendingSsoStart = {
  serverUrl: string;
  state: string;
};

export function usesBrowserSso(connection: { authentication?: { mode?: string } } | null | undefined): boolean {
  return connection?.authentication?.mode === 'browserSso';
}

export function createSsoState(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
}

export function browserSsoStartUrl(serverUrl: string, redirectUri: string, state: string): URL {
  const target = new URL(serverUrl);
  if (target.protocol !== 'http:' && target.protocol !== 'https:') {
    throw new Error('Server URLs must use http or https.');
  }

  target.pathname = `${target.pathname.replace(/\/+$/, '')}/api/auth/sso`;
  target.search = '';
  target.hash = '';
  target.searchParams.set('redirect_uri', redirectUri);
  target.searchParams.set('state', state);
  return target;
}

export function readSsoCallback(href: string): { accessToken: string; state: string } | null {
  try {
    const url = new URL(href);
    const accessToken = url.searchParams.get('access_token');
    const state = url.searchParams.get('state');
    if (!accessToken || !state) return null;
    return { accessToken, state };
  } catch {
    return null;
  }
}

export function rememberSsoStart(serverUrl: string, state: string): void {
  if (typeof sessionStorage === 'undefined') return;
  sessionStorage.setItem(pendingStartKey, JSON.stringify({ serverUrl, state } satisfies PendingSsoStart));
}

export function takePendingSsoStart(): PendingSsoStart | null {
  if (typeof sessionStorage === 'undefined') return null;
  const raw = sessionStorage.getItem(pendingStartKey);
  sessionStorage.removeItem(pendingStartKey);
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as Partial<PendingSsoStart>;
    if (typeof parsed.serverUrl !== 'string' || typeof parsed.state !== 'string') return null;
    return { serverUrl: parsed.serverUrl, state: parsed.state };
  } catch {
    return null;
  }
}
