import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { PublicIconNames, publicIconUri } from './PublicIconProvider';

const iconsRoot = join(dirname(fileURLToPath(import.meta.url)), '../../../../public/icons');

test('public icon uris stay on same-origin static files', () => {
  assert.equal(publicIconUri('apps'), '/icons/apps.svg');
  assert.equal(publicIconUri('download'), '/icons/download.svg');
  assert.equal(publicIconUri('menu'), '/icons/menu.svg');
});

test('every hosted icon is a stroked 24 SVG in public', () => {
  for (const name of PublicIconNames) {
    const svg = readFileSync(join(iconsRoot, `${name}.svg`), 'utf8');
    assert.match(svg, /viewBox="0 0 24 24"/);
    assert.match(svg, /stroke="/);
    assert.equal(publicIconUri(name), `/icons/${name}.svg`);
  }
});
