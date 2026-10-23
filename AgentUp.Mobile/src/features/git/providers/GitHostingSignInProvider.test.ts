import assert from 'node:assert/strict';
import { test } from 'node:test';
import { signInUrlAfterScopeChange, signInUrlForCurrentScope } from './GitHostingSignInProvider';

test('a login result applies only to the scope that started it', () => {
  assert.equal(
    signInUrlForCurrentScope('server-a:ws-1', 'server-a:ws-1', 'https://example.test/authorize'),
    'https://example.test/authorize',
  );
  assert.equal(
    signInUrlForCurrentScope('server-a:ws-1', 'server-a:ws-2', 'https://example.test/authorize'),
    null,
  );
});

test('a saved sign-in URL does not follow a workspace or server change', () => {
  assert.equal(
    signInUrlAfterScopeChange('server-a:ws-1', 'server-a:ws-1', 'https://example.test/authorize'),
    'https://example.test/authorize',
  );
  assert.equal(
    signInUrlAfterScopeChange('server-a:ws-1', 'server-b:ws-1', 'https://example.test/authorize'),
    null,
  );
});
