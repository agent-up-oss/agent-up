import assert from 'node:assert/strict';
import test from 'node:test';
import { followAfterScroll, pacedStart, revealStep } from './MotionProvider';

test('a burst of streamed text flows in over a few frames rather than at once', () => {
  let shown = 0;
  let frames = 0;
  while (shown < 600 && frames < 100) {
    shown = revealStep(shown, 600, 16);
    frames++;
  }
  assert.ok(frames >= 5 && frames <= 20, `took ${frames} frames`);
});

test('the reveal never passes the text that arrived and moves further after a slow frame', () => {
  assert.equal(revealStep(10, 11, 1000), 11);
  assert.equal(revealStep(12, 11, 16), 11);
  assert.ok(revealStep(0, 600, 64) > revealStep(0, 600, 16));
});

test('scrolling up hands the transcript to the reader and reaching the end hands it back', () => {
  assert.equal(followAfterScroll(true, 500, 300, 400, 1200), false, 'moved up');
  assert.equal(followAfterScroll(false, 300, 790, 400, 1200), true, 'back at the end');
  assert.equal(followAfterScroll(true, 300, 500, 400, 1200), true, "the glide's own halfway point");
  assert.equal(followAfterScroll(false, 300, 500, 400, 1200), false, 'a reader scrolling down part way');
});

test('only text that extends what is shown is paced', () => {
  assert.equal(pacedStart('Hello', 'Hello world', true), 'Hello');
  assert.equal(pacedStart('Hello', 'Goodbye', true), 'Goodbye');
  assert.equal(pacedStart('', 'Hello', true), 'Hello', 'a message seen for the first time lands at once');
  assert.equal(pacedStart('Hello', 'Hello world', false), 'Hello world');
});
