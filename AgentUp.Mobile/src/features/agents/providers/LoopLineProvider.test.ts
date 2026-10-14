import assert from 'node:assert/strict';
import test from 'node:test';
import { formatLoopLine } from './LoopLineProvider';

test('formatLoopLine joins phase, task and stop reason', () => {
  const line = formatLoopLine({
    active: true,
    phase: 'blocked',
    taskTitle: 'Ship the review',
    terminal: 'BlockedPolicy',
    stopReason: 'policy',
  });
  assert.equal(line, 'blocked · Ship the review · BlockedPolicy · policy');
});

test('formatLoopLine is null when the session has no loop', () => {
  assert.equal(formatLoopLine(null), null);
});
