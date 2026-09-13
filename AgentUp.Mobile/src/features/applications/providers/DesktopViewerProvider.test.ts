import assert from 'node:assert/strict';
import test from 'node:test';
import { ServerRequestError } from '../../servers/providers/ServerRequestProvider';
import {
  createDesktopViewerUrl,
  shouldRetryDesktopTicket,
  waitForDesktopViewerUrl,
} from './DesktopViewerProvider';

const ticketBody = {
  viewerUrl: '/api/desktop-applications/session/session-1/viewer?ticket=secret',
  expiresAtUtc: '2030-01-01T00:00:00Z',
};

test('creates an authenticated viewer ticket and resolves its server-relative URL', async () => {
  let requestedUrl = '';
  let authorization = '';
  const request = async (input: string | URL | Request, init?: RequestInit) => {
    requestedUrl = String(input);
    authorization = String((init?.headers as Record<string, string>)?.Authorization);
    return new Response(JSON.stringify(ticketBody), { status: 200, headers: { 'Content-Type': 'application/json' } });
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

test('retries viewer tickets while the application is starting', async () => {
  let attempts = 0;
  const delays: number[] = [];
  const request = async () => {
    attempts += 1;
    if (attempts < 3) return new Response('missing', { status: 404 });
    return new Response(JSON.stringify(ticketBody), { status: 200, headers: { 'Content-Type': 'application/json' } });
  };

  const url = await waitForDesktopViewerUrl(
    { url: 'https://agent-up.example', accessToken: 'token' },
    'workspace-1',
    'Sample Desktop',
    {
      applicationState: () => 'Starting',
      request: request as typeof fetch,
      delay: async ms => { delays.push(ms); },
    },
  );

  assert.equal(attempts, 3);
  assert.deepEqual(delays, [200, 200]);
  assert.equal(url, 'https://agent-up.example/api/desktop-applications/session/session-1/viewer?ticket=secret');
});

test('does not retry viewer tickets after the application has failed', async () => {
  let attempts = 0;
  const request = async () => {
    attempts += 1;
    return new Response('missing', { status: 404 });
  };

  await assert.rejects(
    () => waitForDesktopViewerUrl(
      { url: 'https://agent-up.example' },
      'workspace-1',
      'Sample Desktop',
      {
        applicationState: () => 'Failed',
        request: request as typeof fetch,
        delay: async () => { throw new Error('Failed applications must not wait to retry.'); },
      },
    ),
    (error: unknown) => error instanceof ServerRequestError && error.status === 404,
  );
  assert.equal(attempts, 1);
});

test('shouldRetryDesktopTicket only while the Server is bringing the session up', () => {
  assert.equal(shouldRetryDesktopTicket('Starting'), true);
  assert.equal(shouldRetryDesktopTicket('Running'), true);
  assert.equal(shouldRetryDesktopTicket('Failed'), false);
  assert.equal(shouldRetryDesktopTicket('Stopped'), false);
});
