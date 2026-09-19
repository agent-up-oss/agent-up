import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { test } from 'node:test';

import { supervise } from '../harness/supervise.mjs';

// A stack that fails to come up used to report "fetch failed" after waiting a full minute, which
// is true and explains nothing: it was how one run of four lost the identity provider with no
// record of why. These pin the two things that were missing.

test('a process that dies is reported by what it said, not by the timeout', async () => {
  const dying = supervise('test-idp', spawn(process.execPath, ['-e', 'console.error("port 24000 in use"); process.exit(3)']));

  const failure = await dying
    .answers('the test identity provider to answer', () => false, { timeoutMs: 30_000 })
    .then(() => null, cause => cause);

  assert.ok(failure, 'the wait should not have succeeded');
  assert.match(failure.message, /exited with code 3/);
  assert.match(failure.message, /port 24000 in use/, 'what it said has to survive into the failure');
});

test('it gives up as soon as the process is gone rather than waiting out the deadline', async () => {
  const dying = supervise('server', spawn(process.execPath, ['-e', 'process.exit(1)']));

  const started = Date.now();
  await dying.answers('the Agent-Up Server to answer', () => false, { timeoutMs: 30_000 }).catch(() => {});

  assert.ok(Date.now() - started < 10_000, 'a dead process should end the wait immediately');
});

test('a process that answers is not disturbed', async () => {
  // Held open by its own stdin rather than a timer: this suite forbids fixed delays, and a test
  // that needs a live process is no exception to that.
  const alive = supervise('test-idp', spawn(process.execPath, ['-e', 'process.stdin.resume()']));
  try {
    let asked = 0;
    const answer = await alive.answers('the test identity provider to answer', () => ++asked >= 3 && 'ready');
    assert.equal(answer, 'ready');
  } finally {
    alive.child.kill('SIGKILL');
  }
});

test('a process that says nothing still says that much', async () => {
  const silent = supervise('test-idp', spawn(process.execPath, ['-e', 'process.exit(9)']));

  const failure = await silent
    .answers('the test identity provider to answer', () => false, { timeoutMs: 30_000 })
    .then(() => null, cause => cause);

  assert.match(failure.message, /It said nothing at all/);
});
