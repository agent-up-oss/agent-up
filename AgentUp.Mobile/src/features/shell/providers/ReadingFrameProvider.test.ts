import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readingFrameLayout, readingFrameSize } from './ReadingFrameProvider';

test('a phone keeps the full viewport so the frame does not crop its height', () => {
  assert.deepEqual(readingFrameSize(390, 844), { width: 390, height: 844 });
});

test('a wide desktop window sizes the reading column to 4:3 of the window height', () => {
  assert.deepEqual(readingFrameSize(1920, 1080), { width: 1440, height: 1080 });
  assert.deepEqual(readingFrameSize(1440, 900), { width: 1200, height: 900 });
});

test('an already 4:3 viewport is not letterboxed', () => {
  assert.deepEqual(readingFrameSize(1600, 1200), { width: 1600, height: 1200 });
});

test('a missing viewport does not produce a negative frame', () => {
  assert.deepEqual(readingFrameSize(0, 0), { width: 0, height: 0 });
});

test('an ultrawide window still uses a 4:3 column of the window height', () => {
  assert.deepEqual(readingFrameSize(3440, 1440), { width: 1920, height: 1440 });
});

test('a landscape phone still letterboxes content to 4:3 of its height', () => {
  assert.deepEqual(readingFrameSize(844, 390), { width: 520, height: 390 });
});

test('an unmeasured viewport fills its parent instead of clipping to zero', () => {
  assert.deepEqual(readingFrameLayout(0, 0), { kind: 'fill' });
});

test('a phone-width window keeps chrome and content full bleed', () => {
  assert.deepEqual(readingFrameLayout(390, 844), { kind: 'fill' });
  assert.deepEqual(readingFrameLayout(1600, 1200), { kind: 'fill' });
});

test('a wide window constrains only the content column, not the chrome', () => {
  assert.deepEqual(readingFrameLayout(1920, 1080), { kind: 'column', width: 1440 });
  assert.deepEqual(readingFrameLayout(1440, 900), { kind: 'column', width: 1200 });
  assert.deepEqual(readingFrameLayout(3440, 1440), { kind: 'column', width: 1920 });
});
