import assert from 'node:assert/strict';
import test from 'node:test';
import { createDesktopViewerUrl } from './DesktopViewerProvider';

test('creates an authenticated viewer ticket and resolves its server-relative URL', async () => {
  let requestedUrl = '';
  let authorization = '';
  const request = async (input: string | URL | Request, init?: RequestInit) => {
    requestedUrl = String(input);
    authorization = String((init?.headers as Record<string, string>)?.Authorization);
    return new Response(JSON.stringify({
      viewerUrl: '/api/desktop-applications/session/session-1/viewer?ticket=secret',
      expiresAtUtc: '2030-01-01T00:00:00Z',
    }), { status: 200, headers: { 'Content-Type': 'application/json' } });
  };

  const url = await createDesktopViewerUrl(
    { url: 'https://agent-up.example', accessToken: 'token' },
    'workspace/id',
    'My App',
    request as typeof fetch,
  );

  assert.equal(requestedUrl, 'https://agent-up.example/api/desktop-applications/workspace%2Fid/My%20App/viewer-ticket');
  assert.equal(authorization, 'Bearer token');
  assert.equal(url, 'https://agent-up.example/api/desktop-applications/session/session-1/viewer?ticket=secret');
});
