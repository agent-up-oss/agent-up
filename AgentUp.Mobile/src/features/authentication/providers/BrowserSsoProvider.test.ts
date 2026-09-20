import assert from 'node:assert/strict';
import test from 'node:test';
import { browserSsoStartUrl, readSsoCallback, usesBrowserSso } from './BrowserSsoProvider';

test('usesBrowserSso follows connection authentication mode', () => {
  assert.equal(usesBrowserSso({ authentication: { mode: 'browserSso' } }), true);
  assert.equal(usesBrowserSso({ authentication: { mode: 'localAdministrator' } }), false);
  assert.equal(usesBrowserSso(null), false);
});

test('browserSsoStartUrl points at the server SSO start route', () => {
  assert.equal(
    browserSsoStartUrl('http://127.0.0.1:5288/', 'http://localhost:8081/connect', 'abc123').href,
    'http://127.0.0.1:5288/api/auth/sso?redirect_uri=http%3A%2F%2Flocalhost%3A8081%2Fconnect&state=abc123',
  );
});

test('browserSsoStartUrl rejects non-http(s) servers', () => {
  assert.throws(() => browserSsoStartUrl('javascript:alert(1)', 'http://localhost:8081/connect', 'abc123'));
  assert.throws(() => browserSsoStartUrl('data:text/html,hi', 'http://localhost:8081/connect', 'abc123'));
});

test('readSsoCallback requires both the token and the matching state', () => {
  assert.deepEqual(
    readSsoCallback('http://localhost:8081/connect?access_token=sso-token&state=abc123'),
    { accessToken: 'sso-token', state: 'abc123' },
  );
  assert.equal(readSsoCallback('http://localhost:8081/connect?access_token=sso-token'), null);
  assert.equal(readSsoCallback('not a url'), null);
});
