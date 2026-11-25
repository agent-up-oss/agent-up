import assert from 'node:assert/strict';
import test from 'node:test';
import { emitSceneNative, parseNodes } from '../scripts/lib/scene.mjs';

test('a catalog button keeps data-au-id as the action id', () => {
  const [button] = parseNodes('<button data-au-action="forge-tab" data-au-id="forgejo">Forgejo</button>');
  assert.equal(button.action, 'forge-tab');
  assert.equal(button.id, 'forgejo');
});

test('a scene action names the catalog node when the row has no id', () => {
  const source = emitSceneNative({ scenes: [], templates: {}, contracts: [] });
  assert.match(source, /item\?\.id \?\? node\.id \?\? null/);
});
