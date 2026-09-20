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
    browserSsoStartUrl('http://127.0.0.1:5288/', 'http://localhost:8081/connect'),
    'http://127.0.0.1:5288/api/auth/sso?redirect_uri=http%3A%2F%2Flocalhost%3A8081%2Fconnect',
  );
});

test('readAccessToken reads the loopback callback query', () => {
  assert.equal(
    readAccessToken('http://localhost:8081/connect?access_token=sso-token'),
    'sso-token',
  );
  assert.equal(readAccessToken('not a url'), null);
});
