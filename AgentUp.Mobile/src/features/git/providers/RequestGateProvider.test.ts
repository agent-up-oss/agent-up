import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createRequestGate } from './RequestGateProvider';

test('the newest request is the current one', () => {
  const gate = createRequestGate();

  const first = gate.begin();
  assert.equal(gate.isCurrent(first), true);

  const second = gate.begin();
  assert.equal(gate.isCurrent(first), false, 'a superseded request must not apply its result');
  assert.equal(gate.isCurrent(second), true);
});

test('gates are independent of each other', () => {
  const tree = createRequestGate();
  const diff = createRequestGate();

  const treeTicket = tree.begin();
  diff.begin();
  diff.begin();

  assert.equal(tree.isCurrent(treeTicket), true, 'opening files must not cancel the tree load');
});

test('a ticket stays current across repeated checks', () => {
  const gate = createRequestGate();
  const ticket = gate.begin();

  assert.equal(gate.isCurrent(ticket), true);
  assert.equal(gate.isCurrent(ticket), true);
});
