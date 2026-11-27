import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  changeSheetPeekMinimum,
  gitChangeSheetCount,
  gitChangeSheetDetents,
  gitChangeSheetDraggedHeight,
  gitChangeSheetHeight,
  gitChangeSheetTakesDrag,
  gitChangesUseSheet,
  settleGitChangeSheet,
} from './GitChangeSheetProvider';

const detents = { peek: 200, full: 800 };

test('the sheet peeks at a quarter of the page and opens to all of it', () => {
  assert.deepEqual(gitChangeSheetDetents(800), { peek: 200, full: 800 });
});

test('a short page still peeks at the heading and a few rows, never past full', () => {
  assert.equal(gitChangeSheetDetents(400).peek, changeSheetPeekMinimum);
  const tiny = gitChangeSheetDetents(100);
  assert.equal(tiny.peek, tiny.full);
});

test('each detent names its height', () => {
  assert.equal(gitChangeSheetHeight('peek', detents), 200);
  assert.equal(gitChangeSheetHeight('full', detents), 800);
});

test('at the peek any vertical drag moves the sheet', () => {
  assert.equal(gitChangeSheetTakesDrag({ detent: 'peek', scrollTop: 0, dx: 0, dy: -20 }), true);
  assert.equal(gitChangeSheetTakesDrag({ detent: 'peek', scrollTop: 0, dx: 0, dy: 20 }), true);
});

test('a tap or a sideways move stays with the list, so a checkbox works at the peek', () => {
  assert.equal(gitChangeSheetTakesDrag({ detent: 'peek', scrollTop: 0, dx: 0, dy: 3 }), false);
  assert.equal(gitChangeSheetTakesDrag({ detent: 'peek', scrollTop: 0, dx: 40, dy: 12 }), false);
});

test('at full height the list scrolls until it is back at its top', () => {
  assert.equal(gitChangeSheetTakesDrag({ detent: 'full', scrollTop: 0, dx: 0, dy: -20 }), false);
  assert.equal(gitChangeSheetTakesDrag({ detent: 'full', scrollTop: 90, dx: 0, dy: 20 }), false);
  assert.equal(gitChangeSheetTakesDrag({ detent: 'full', scrollTop: 0, dx: 0, dy: 20 }), true);
});

test('the sheet follows the finger and resists past either detent instead of closing', () => {
  assert.equal(gitChangeSheetDraggedHeight(200, -300, detents), 500);
  assert.equal(gitChangeSheetDraggedHeight(200, 100, detents), 180);
  assert.equal(gitChangeSheetDraggedHeight(800, -100, detents), 820);
});

test('a slow release settles at the nearer detent', () => {
  assert.equal(settleGitChangeSheet({ height: 600, velocity: 0, detents }), 'full');
  assert.equal(settleGitChangeSheet({ height: 350, velocity: 0, detents }), 'peek');
});

test('a flick goes its way and a flick down never closes the sheet', () => {
  assert.equal(settleGitChangeSheet({ height: 260, velocity: -1.4, detents }), 'full');
  assert.equal(settleGitChangeSheet({ height: 760, velocity: 1.4, detents }), 'peek');
  assert.equal(settleGitChangeSheet({ height: 190, velocity: 2, detents }), 'peek');
});

test('the heading counts what the next commit takes', () => {
  assert.equal(gitChangeSheetCount(2, 5), '2 of 5 selected');
  assert.equal(gitChangeSheetCount(0, 0), 'No changes');
});

test('only a phone-width window rests the change list in a sheet', () => {
  assert.equal(gitChangesUseSheet(390), true);
  assert.equal(gitChangesUseSheet(430), true);
  assert.equal(gitChangesUseSheet(768), false);
  assert.equal(gitChangesUseSheet(1440), false);
});

test('a drag that starts past a detent follows the finger instead of resisting twice', () => {
  const at = (start: number, dy: number) => Math.round(gitChangeSheetDraggedHeight(start, dy, detents));
  assert.equal(at(190, 0), 190);
  assert.equal(at(190, 20), 186);
  assert.equal(at(820, 0), 820);
  assert.equal(at(820, 50), 810);
  assert.equal(at(820, 200), 700);
});
