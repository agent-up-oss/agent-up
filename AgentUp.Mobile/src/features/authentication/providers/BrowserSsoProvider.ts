const pendingServerKey = 'agent-up.sso.server';

export function usesBrowserSso(connection: { authentication?: { mode?: string } } | null | undefined): boolean {
  return connection?.authentication?.mode === 'browserSso';
}

export function browserSsoStartUrl(serverUrl: string, redirectUri: string): URL {
  const target = new URL(serverUrl);
  if (target.protocol !== 'http:' && target.protocol !== 'https:') {
    throw new Error('Server URLs must use http or https.');
  }

  target.pathname = `${target.pathname.replace(/\/+$/, '')}/api/auth/sso`;
  target.search = '';
  target.hash = '';
  target.searchParams.set('redirect_uri', redirectUri);
  return target;
}

export function readAccessToken(href: string): string | null {
  try {
    return new URL(href).searchParams.get('access_token');
  } catch {
    return null;
  }
}

export function rememberSsoServer(serverUrl: string): void {
  if (typeof sessionStorage === 'undefined') return;
  sessionStorage.setItem(pendingServerKey, serverUrl);
}

export function takePendingSsoServer(): string | null {
  if (typeof sessionStorage === 'undefined') return null;
  const value = sessionStorage.getItem(pendingServerKey);
  sessionStorage.removeItem(pendingServerKey);
  return value;
}
