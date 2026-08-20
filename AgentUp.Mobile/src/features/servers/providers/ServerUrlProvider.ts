export function normalizeServerUrl(value: string): string {
  const trimmed = value.trim();
  let parsed: URL;

  try {
    parsed = new URL(trimmed);
  } catch {
    throw new Error('Enter a valid server URL.');
  }

  if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:')
    throw new Error('Server URLs must use http or https.');
  if (parsed.username || parsed.password || parsed.search || parsed.hash)
    throw new Error('Enter only the server base URL.');

  parsed.pathname = parsed.pathname.replace(/\/+$/, '');
  return parsed.toString().replace(/\/$/, '');
}

export async function probeServer(url: string, request: typeof fetch = fetch): Promise<void> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 8000);
  try {
    const response = await request(`${url}/api/workspaces`, {
      method: 'GET',
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    });
    if (!response.ok)
      throw new Error(`Server returned ${response.status}.`);
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError')
      throw new Error('The server did not respond in time.');
    throw error instanceof Error ? error : new Error('Could not connect to the server.');
  } finally {
    clearTimeout(timeout);
  }
}
