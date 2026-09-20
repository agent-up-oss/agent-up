const pendingServerKey = 'agent-up.sso.server';

export function browserSsoStartUrl(serverUrl: string, redirectUri: string): string {
  return `${serverUrl.replace(/\/$/, '')}/api/auth/sso?redirect_uri=${encodeURIComponent(redirectUri)}`;
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
