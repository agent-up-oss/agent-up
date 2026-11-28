import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  memberSheetDraggedOffset,
  memberSheetHandleHeight,
  memberSheetTakesDrag,
  settleMemberSheet,
} from './MemberSheetProvider';

test('a drag down on the head moves the member sheet', () => {
  assert.equal(memberSheetTakesDrag({ fromTop: 40, dx: 0, dy: 20, innerExpanded: false }), true);
});

test('a drag that starts on the page below the head stays with the page', () => {
  assert.equal(memberSheetTakesDrag({ fromTop: memberSheetHandleHeight + 1, dx: 0, dy: 20, innerExpanded: false }), false);
});

test('a tap, a sideways move, or a drag up on the head is not the sheet\'s', () => {
  assert.equal(memberSheetTakesDrag({ fromTop: 40, dx: 0, dy: 3, innerExpanded: false }), false);
  assert.equal(memberSheetTakesDrag({ fromTop: 40, dx: 40, dy: 12, innerExpanded: false }), false);
  assert.equal(memberSheetTakesDrag({ fromTop: 40, dx: 0, dy: -20, innerExpanded: false }), false);
});

test('a sheet open over the member sheet takes the drag, so it closes first', () => {
  assert.equal(memberSheetTakesDrag({ fromTop: 40, dx: 0, dy: 20, innerExpanded: true }), false);
});

test('the sheet follows a finger down and resists a pull up', () => {
  assert.equal(memberSheetDraggedOffset(120), 120);
  assert.equal(memberSheetDraggedOffset(-100), -20);
});

test('a flick down closes the sheet whatever the distance', () => {
  assert.equal(settleMemberSheet({ offset: 20, velocity: 1.2, height: 800 }), 'closed');
});

test('a slow release closes the sheet only past a third of the way', () => {
  assert.equal(settleMemberSheet({ offset: 200, velocity: 0.1, height: 800 }), 'open');
  assert.equal(settleMemberSheet({ offset: 300, velocity: 0.1, height: 800 }), 'closed');
});

test('a flick back up reopens a sheet dragged far down', () => {
  assert.equal(settleMemberSheet({ offset: 500, velocity: -1.2, height: 800 }), 'open');
});
