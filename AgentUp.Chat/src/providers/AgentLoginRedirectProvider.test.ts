import assert from 'node:assert/strict';
import test from 'node:test';

import { shouldInterceptNavigation } from './AgentLoginRedirectProvider';

// The WebView's navigation guard is the whole reason a redirect sign-in can finish on a phone.
// Getting it wrong either drops the callback or stops navigations it should have let through.
test('the callback navigation is intercepted across both loopback spellings', () => {
  const redirectUri = 'http://localhost:1455/auth/callback';

  assert.equal(shouldInterceptNavigation('http://localhost:1455/auth/callback?code=abc&state=xyz', redirectUri), true);
  assert.equal(shouldInterceptNavigation('http://127.0.0.1:1455/auth/callback?code=abc', redirectUri), true);
});

test('navigations that are not the callback are allowed to continue loading', () => {
  const redirectUri = 'http://localhost:1455/auth/callback';

  assert.equal(
    shouldInterceptNavigation('https://auth.openai.com/oauth/authorize?client_id=x', redirectUri),
    false,
    'the provider pages have to load normally',
  );
  assert.equal(
    shouldInterceptNavigation('https://auth.openai.com/login', redirectUri),
    false,
    'so do any pages it redirects through',
  );
  assert.equal(
    shouldInterceptNavigation('http://localhost:9999/auth/callback?code=abc', redirectUri),
    false,
    'a different port is a different listener',
  );
  assert.equal(
    shouldInterceptNavigation('http://localhost:1455/something-else', redirectUri),
    false,
    'a different path is a different endpoint',
  );
});

test('a lookalike host on the public internet is never treated as the callback', () => {
  assert.equal(
    shouldInterceptNavigation('https://localhost.evil.example.com/auth/callback?code=abc', 'http://localhost:1455/auth/callback'),
    false,
  );
});
