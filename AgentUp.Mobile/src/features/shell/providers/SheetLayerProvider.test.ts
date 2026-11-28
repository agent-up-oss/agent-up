import assert from 'node:assert/strict';
import { test } from 'node:test';
import { sheetLayerFrame } from './SheetLayerProvider';

test('a sheet in the layer opens to the whole layer and rests above the tab bar under its page', () => {
  // The layer is the member sheet; the Git page ends 64 points above it, where the tabs are.
  const frame = sheetLayerFrame({ layer: { y: 0, height: 800 }, page: { y: 90, height: 646 }, topInset: 0 });
  assert.deepEqual(frame, { height: 800, bottomInset: 64 });
});

test('a sheet never opens under the phone chrome the layer reaches into', () => {
  const frame = sheetLayerFrame({ layer: { y: 0, height: 844 }, page: { y: 100, height: 744 }, topInset: 47 });
  assert.deepEqual(frame, { height: 797, bottomInset: 0 });
});

test('a layer below the phone chrome gives the sheet all of its height', () => {
  const frame = sheetLayerFrame({ layer: { y: 60, height: 700 }, page: { y: 120, height: 640 }, topInset: 47 });
  assert.deepEqual(frame, { height: 700, bottomInset: 0 });
});

test('a moving layer measures the same frame, since only the difference counts', () => {
  const still = sheetLayerFrame({ layer: { y: 0, height: 800 }, page: { y: 90, height: 646 }, topInset: 0 });
  const sliding = sheetLayerFrame({ layer: { y: 300, height: 800 }, page: { y: 390, height: 646 }, topInset: 0 });
  assert.deepEqual(sliding, still);
});
