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

test('current reads the ticket in force without claiming a new one', () => {
  const gate = createRequestGate();
  const begun = gate.begin();

  assert.equal(gate.current(), begun, 'reading must not move the gate on');
  assert.equal(gate.isCurrent(begun), true);
});

test('a ticket read before the gate advances is stale afterwards', () => {
  const gate = createRequestGate();

  // A commit reads the ticket its context is on; switching workspace advances the gate.
  const ticket = gate.current();
  assert.equal(gate.isCurrent(ticket), true, 'still the same context');

  gate.begin();
  assert.equal(gate.isCurrent(ticket), false, 'work from the previous context must not apply');
});
