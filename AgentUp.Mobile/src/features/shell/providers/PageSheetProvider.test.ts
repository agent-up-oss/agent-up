import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  pageSheetDraggedOffset,
  pageSheetHandleFallback,
  pageSheetTakesDrag,
  settlePageSheet,
} from './PageSheetProvider';

test('a drag down on the head moves the sheet', () => {
  assert.equal(pageSheetTakesDrag({ fromTop: 40, handleHeight: 82, dx: 0, dy: 20 }), true);
});

test('a drag that starts below the head stays with the page or the sheet docked there', () => {
  assert.equal(pageSheetTakesDrag({ fromTop: 83, handleHeight: 82, dx: 0, dy: 20 }), false);
});

test('an unmeasured head still has a handle', () => {
  assert.equal(pageSheetTakesDrag({ fromTop: pageSheetHandleFallback, handleHeight: null, dx: 0, dy: 20 }), true);
  assert.equal(pageSheetTakesDrag({ fromTop: pageSheetHandleFallback + 1, handleHeight: 0, dx: 0, dy: 20 }), false);
});

test('a tap, a sideways move, or a drag up on the head is not the sheet\'s', () => {
  assert.equal(pageSheetTakesDrag({ fromTop: 40, handleHeight: 82, dx: 0, dy: 3 }), false);
  assert.equal(pageSheetTakesDrag({ fromTop: 40, handleHeight: 82, dx: 40, dy: 12 }), false);
  assert.equal(pageSheetTakesDrag({ fromTop: 40, handleHeight: 82, dx: 0, dy: -20 }), false);
});

test('the sheet follows a finger down and resists a pull up', () => {
  assert.equal(pageSheetDraggedOffset(120), 120);
  assert.equal(pageSheetDraggedOffset(-100), -20);
});

test('a flick down closes the sheet whatever the distance', () => {
  assert.equal(settlePageSheet({ offset: 20, velocity: 1.2, height: 800 }), 'closed');
});

test('a slow release closes the sheet only past a third of the way', () => {
  assert.equal(settlePageSheet({ offset: 200, velocity: 0.1, height: 800 }), 'open');
  assert.equal(settlePageSheet({ offset: 300, velocity: 0.1, height: 800 }), 'closed');
});

test('a flick back up reopens a sheet dragged far down', () => {
  assert.equal(settlePageSheet({ offset: 500, velocity: -1.2, height: 800 }), 'open');
});
