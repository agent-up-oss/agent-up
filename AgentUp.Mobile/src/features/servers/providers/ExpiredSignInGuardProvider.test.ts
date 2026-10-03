import assert from 'node:assert/strict';
import { test } from 'node:test';
import { nextExpiredSignInGuard } from './ExpiredSignInGuardProvider';

test('clearing requiresSignIn resets the expired-sign-in guard', () => {
  const next = nextExpiredSignInGuard(false, true, true);
  assert.deepEqual(next, { applied: false, run: false });
});

test('the first expiry on an active server runs once', () => {
  const first = nextExpiredSignInGuard(true, true, false);
  assert.deepEqual(first, { applied: true, run: true });

  const again = nextExpiredSignInGuard(true, true, first.applied);
  assert.deepEqual(again, { applied: true, run: false });
});

test('an expiry without an active server waits', () => {
  const next = nextExpiredSignInGuard(true, false, false);
  assert.deepEqual(next, { applied: false, run: false });
});

test('a later expiry after sign-in succeeds can run again', () => {
  const afterSuccess = nextExpiredSignInGuard(false, true, true);
  const later = nextExpiredSignInGuard(true, true, afterSuccess.applied);
  assert.deepEqual(later, { applied: true, run: true });
});
