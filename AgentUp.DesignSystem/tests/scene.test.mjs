import assert from 'node:assert/strict';
import test from 'node:test';
import { emitSceneNative, parseNodes } from '../scripts/lib/scene.mjs';

test('a catalog button keeps data-au-id as the action id', () => {
  const [button] = parseNodes('<button data-au-action="forge-tab" data-au-id="forgejo">Forgejo</button>');
  assert.equal(button.action, 'forge-tab');
  assert.equal(button.id, 'forgejo');
});

test('a catalog textarea is a multiline field', () => {
  const [box] = parseNodes('<textarea data-au-action="system-prompt" data-au-text="systemPrompt" placeholder="System prompt"></textarea>');
  assert.equal(box.action, 'system-prompt');
  assert.equal(box.text, 'systemPrompt');
  assert.equal(box.multiline, true);
  assert.equal(box.placeholder, 'System prompt');
});

test('a scene action names the catalog node when the row has no id', () => {
  const source = emitSceneNative({ scenes: [], templates: {}, contracts: [] });
  assert.match(source, /item\?\.id \?\? node\.id \?\? null/);
});
