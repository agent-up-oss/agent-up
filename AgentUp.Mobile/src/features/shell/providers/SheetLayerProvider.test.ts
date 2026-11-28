import assert from 'node:assert/strict';
import { test } from 'node:test';
import { sheetDockOffset, sheetLayerFrame } from './SheetLayerProvider';

test('a sheet in the layer docks under the head and opens over the tab bar under its page', () => {
  // The layer is the member sheet; its head ends at 90 and the Git page ends 64 points above the
  // layer's bottom, where the tabs are.
  const frame = sheetLayerFrame({ layer: { y: 0, height: 800 }, page: { y: 90, height: 646 }, topInset: 0 });
  assert.deepEqual(frame, { height: 710, bottomInset: 64 });
});

test('a sheet never opens under the phone chrome', () => {
  const frame = sheetLayerFrame({ layer: { y: 0, height: 844 }, page: { y: 20, height: 824 }, topInset: 47 });
  assert.deepEqual(frame, { height: 797, bottomInset: 0 });
});

test('a moving layer measures the same frame, since only the difference counts', () => {
  const still = sheetLayerFrame({ layer: { y: 0, height: 800 }, page: { y: 90, height: 646 }, topInset: 0 });
  const sliding = sheetLayerFrame({ layer: { y: 300, height: 800 }, page: { y: 390, height: 646 }, topInset: 0 });
  assert.deepEqual(sliding, still);
});

test('a sheet docks as far below the layer top as the head reaches', () => {
  assert.equal(sheetDockOffset({ y: 47, height: 797 }, { y: 129, height: 0 }), 82);
  assert.equal(sheetDockOffset({ y: 347, height: 797 }, { y: 429, height: 0 }), 82);
});
