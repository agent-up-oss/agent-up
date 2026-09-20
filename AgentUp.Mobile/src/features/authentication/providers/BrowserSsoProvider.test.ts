import assert from 'node:assert/strict';
import test from 'node:test';
import { browserSsoStartUrl, readAccessToken, usesBrowserSso } from './BrowserSsoProvider';

test('usesBrowserSso follows connection authentication mode', () => {
  assert.equal(usesBrowserSso({ authentication: { mode: 'browserSso' } }), true);
  assert.equal(usesBrowserSso({ authentication: { mode: 'localAdministrator' } }), false);
  assert.equal(usesBrowserSso(null), false);
});

test('browserSsoStartUrl points at the server SSO start route', () => {
  assert.equal(
    browserSsoStartUrl('http://127.0.0.1:5288/', 'http://localhost:8081/connect').href,
    'http://127.0.0.1:5288/api/auth/sso?redirect_uri=http%3A%2F%2Flocalhost%3A8081%2Fconnect',
  );
});

test('browserSsoStartUrl rejects non-http(s) servers', () => {
  assert.throws(() => browserSsoStartUrl('javascript:alert(1)', 'http://localhost:8081/connect'));
  assert.throws(() => browserSsoStartUrl('data:text/html,hi', 'http://localhost:8081/connect'));
});

test('readAccessToken reads the loopback callback query', () => {
  assert.equal(
    readAccessToken('http://localhost:8081/connect?access_token=sso-token'),
    'sso-token',
  );
  assert.equal(readAccessToken('not a url'), null);
});
