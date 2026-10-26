import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  beginAttentionPingScript,
  beginMouseMoveScript,
  checkTextScript,
  completeClickScript,
  replayTarget,
} from './ValidationReplayScripts';

test('replayTarget keeps selectors and wraps a bare css string', () => {
  assert.deepEqual(replayTarget('button[data-add="mug"]'), { selector: 'button[data-add="mug"]' });
  assert.equal(replayTarget({ text: 'Add to cart' }).text, 'Add to cart');
});

test('mouse, ping, and click scripts inject the cursor overlay and click the target', () => {
  const target = { selector: '#place', name: 'Place order' };
  const move = beginMouseMoveScript(target);
  const ping = beginAttentionPingScript(target);
  const click = completeClickScript(target);
  assert.equal(move.includes('__agentUpMouse'), true);
  assert.equal(ping.includes('__agentUpClickRing'), true);
  assert.equal(click.includes('.click()'), true);
  assert.equal(click.includes('#place'), true);
});

test('checkTextScript looks for visible page text', () => {
  assert.equal(checkTextScript('Harbor Mug').includes('Harbor Mug'), true);
});

test('script literals escape line separators so they cannot break out of JS strings', () => {
  const script = checkTextScript('Harbor\u2028Mug');
  assert.equal(script.includes('\u2028'), false);
  assert.equal(script.includes('\\u2028'), true);
});
