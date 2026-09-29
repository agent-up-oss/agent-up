export const fakeServerId = 'fake';
export const fakeServerUrl = 'http://127.0.0.1:9';
export const fakeServerDisplayName = 'Demo';

export function normalizeFakeServerUrl(url: string): string {
  return url.trim().replace(/\/+$/, '');
}

export function isFakeServerUrl(url: string | null | undefined): boolean {
  return !!url && normalizeFakeServerUrl(url).toLowerCase() === fakeServerUrl;
}

export function isFakeServerId(id: string | null | undefined): boolean {
  return id === fakeServerId;
}
