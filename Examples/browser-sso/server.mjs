import { createServer } from 'node:http';
import { randomBytes } from 'node:crypto';
import { fileURLToPath } from 'node:url';

const DISPLAY_NAME = 'Shared Server';
const FEATURES = {
  'server.read': true,
  'workspace.read': true,
  'workspace.create': false,
  'workspace.start': true,
  'workspace.stop': true,
  'workspace.delete': false,
  'application.read': true,
  'application.operate': true,
  'agent.read': true,
  'agent.prompt': true,
  'agent.permission.respond': true,
  'git.read': true,
  'git.write': false,
  'diagnostics.read': true,
  'browser.control': true,
};

const tokens = new Set();

export function startBrowserSsoExample({ host = '127.0.0.1', port = 0 } = {}) {
  const server = createServer((request, response) => {
    if (request.method === 'OPTIONS') {
      response.writeHead(204, corsHeaders());
      response.end();
      return;
    }

    const url = new URL(request.url ?? '/', `http://${request.headers.host ?? '127.0.0.1'}`);
    route(request, response, url).catch(error => {
      write(response, 500, { title: 'Internal error', detail: error instanceof Error ? error.message : String(error) });
    });
  });

  return new Promise((resolve, reject) => {
    server.listen(port, host, () => {
      const address = server.address();
      if (!address || typeof address === 'string') {
        reject(new Error('The example did not bind a TCP port.'));
        return;
      }

      resolve({
        origin: `http://${host}:${address.port}`,
        dispose: () => new Promise(done => server.close(done)),
      });
    });
    server.once('error', reject);
  });
}

async function route(request, response, url) {
  if (url.pathname === '/health' && request.method === 'GET') {
    write(response, 200, { ok: true });
    return;
  }

  if (url.pathname === '/api/connection' && request.method === 'GET') {
    write(response, 200, {
      apiVersion: '1',
      connectionId: 'shared',
      kind: 'selfHosted',
      displayName: DISPLAY_NAME,
      authentication: {
        mode: 'browserSso',
        prompt: 'Sign in with your identity provider.',
        identifierRequired: false,
      },
      workspacePresentation: 'serverScoped',
    });
    return;
  }

  if (url.pathname === '/api/auth/status' && request.method === 'GET') {
    write(response, 200, { authenticationRequired: true });
    return;
  }

  if (url.pathname === '/api/auth/login' && request.method === 'POST') {
    write(response, 400, { title: 'Browser sign-in required', detail: 'This Server uses GET /api/auth/sso instead of a password.' });
    return;
  }

  if (url.pathname === '/api/auth/sso' && request.method === 'GET') {
    const redirectUri = url.searchParams.get('redirect_uri') ?? '';
    if (!isLoopbackRedirect(redirectUri)) {
      write(response, 400, { title: 'Invalid redirect_uri', detail: 'redirect_uri must be a loopback http(s) URL.' });
      return;
    }
    writeSignInPage(response);
    return;
  }

  if (url.pathname === '/api/auth/sso' && request.method === 'POST') {
    const body = await readBody(request);
    const redirectUri = body.get('redirect_uri') ?? '';
    if (!isLoopbackRedirect(redirectUri)) {
      write(response, 400, { title: 'Invalid redirect_uri', detail: 'redirect_uri must be a loopback http(s) URL.' });
      return;
    }
    const token = `demo.${randomBytes(16).toString('hex')}`;
    tokens.add(token);
    const target = new URL(redirectUri);
    target.searchParams.set('access_token', token);
    const state = body.get('state') ?? '';
    if (state) target.searchParams.set('state', state);
    response.writeHead(302, { location: target.toString(), ...corsHeaders() });
    response.end();
    return;
  }

  const bearer = bearerToken(request);
  if (url.pathname === '/api/entitlements' && request.method === 'GET') {
    if (!bearer || !tokens.has(bearer)) {
      write(response, 401, { title: 'Unauthorized' });
      return;
    }
    write(response, 200, {
      apiVersion: '1',
      connectionId: 'shared',
      subject: 'demo-user',
      source: 'selfHosted',
      edition: 'community',
      displayName: DISPLAY_NAME,
      billing: 'free',
      revision: 'example',
      expiresAt: null,
      features: Object.fromEntries(Object.entries(FEATURES).map(([key, available]) => [key, { available }])),
      limits: {},
    });
    return;
  }

  if (url.pathname === '/api/workspaces' && request.method === 'GET') {
    if (!bearer || !tokens.has(bearer)) {
      write(response, 401, { title: 'Unauthorized' });
      return;
    }
    write(response, 200, []);
    return;
  }

  write(response, 404, { title: 'Not found' });
}

export function isLoopbackRedirect(value) {
  try {
    const url = new URL(value);
    if (url.protocol !== 'http:' && url.protocol !== 'https:') return false;
    return url.hostname === 'localhost' || url.hostname === '127.0.0.1' || url.hostname === '::1' || url.hostname === '[::1]';
  } catch {
    return false;
  }
}

const SIGN_IN_PAGE = `<!doctype html>
<html lang="en">
<meta charset="utf-8">
<title>Sign in to Shared Server</title>
<body>
  <h1>Sign in to Shared Server</h1>
  <p>This example identity front door issues a one-time Agent-Up access token. It is not an identity vendor SDK.</p>
  <form method="post" action="/api/auth/sso">
    <input type="hidden" name="redirect_uri" id="sso-redirect">
    <input type="hidden" name="state" id="sso-state">
    <button id="sso-continue" type="submit">Continue</button>
  </form>
  <script>
    const params = new URLSearchParams(window.location.search);
    document.getElementById('sso-redirect').value = params.get('redirect_uri') ?? '';
    document.getElementById('sso-state').value = params.get('state') ?? '';
  </script>
</body>
</html>`;

function bearerToken(request) {
  const header = request.headers.authorization ?? '';
  const space = header.indexOf(' ');
  if (space < 0) return '';
  if (header.slice(0, space).toLowerCase() !== 'bearer') return '';
  return header.slice(space + 1).trim();
}

function corsHeaders() {
  return {
    'access-control-allow-origin': '*',
    'access-control-allow-headers': 'Authorization, Content-Type, Accept',
    'access-control-allow-methods': 'GET, POST, OPTIONS',
  };
}

function write(response, status, body) {
  const payload = JSON.stringify(body);
  response.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'content-length': Buffer.byteLength(payload),
    ...corsHeaders(),
  });
  response.end(payload);
}

function writeSignInPage(response) {
  response.writeHead(200, {
    'content-type': 'text/html; charset=utf-8',
    'content-length': Buffer.byteLength(SIGN_IN_PAGE),
    ...corsHeaders(),
  });
  response.end(SIGN_IN_PAGE);
}

async function readBody(request) {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  return new URLSearchParams(Buffer.concat(chunks).toString('utf8'));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const port = Number.parseInt(process.env.PORT ?? '8787', 10);
  const started = await startBrowserSsoExample({ port });
  console.log(`Browser SSO example listening on ${started.origin}`);
}
