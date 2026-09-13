import assert from 'node:assert/strict';
import test from 'node:test';
import { applicationHttpUrl, browserViewerUrl, navigateApplicationBrowser } from './ApplicationBrowserProvider';

test('selects the first HTTP port without exposing it to the mobile device', () => {
  const url = applicationHttpUrl({ name: 'web', state: 'Running', allocatedPorts: [
    { allocatedPort: 5442, protocol: 'tcp' }, { allocatedPort: 6111, protocol: 'HTTP' },
  ] });
  assert.equal(url, 'http://127.0.0.1:6111/');
});

test('places the bearer credential in a fragment rather than the logged request URL', () => {
  const url = browserViewerUrl({ url: 'https://agent.example', accessToken: 'a+b/c=' }, 'work/tree');
  assert.equal(url, 'https://agent.example/api/browser/rdp-viewer?workspaceId=work%2Ftree#access_token=a%2Bb%2Fc%3D');
  assert.equal(new URL(url).searchParams.has('access_token'), false);
});

test('asks the server-side browser to navigate to its loopback application port', async () => {
  let requestedUrl = '';
  let authorization = '';
  const request = (async (input: string | URL | Request, init?: RequestInit) => {
    requestedUrl = String(input);
    authorization = String((init?.headers as Record<string, string>).Authorization);
    return new Response(null, { status: 204 });
  }) as typeof fetch;

  await navigateApplicationBrowser(
    { url: 'https://agent.example', accessToken: 'secret' },
    'workspace/id',
    { name: 'web', state: 'Running', allocatedPorts: [{ allocatedPort: 8080, protocol: 'http' }] },
    request,
  );

  assert.equal(requestedUrl, 'https://agent.example/api/browser/navigate/workspace%2Fid?url=http%3A%2F%2F127.0.0.1%3A8080%2F&reloadIfSameUrl=false');
  assert.equal(authorization, 'Bearer secret');
});

test('rejects applications without an HTTP display', async () => {
  await assert.rejects(
    navigateApplicationBrowser(
      { url: 'https://agent.example' }, 'workspace',
      { name: 'database', state: 'Running', allocatedPorts: [{ allocatedPort: 5432, protocol: 'tcp' }] },
    ),
    /does not expose an HTTP port/,
  );
});
