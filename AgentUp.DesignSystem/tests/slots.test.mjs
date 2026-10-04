import assert from 'node:assert/strict';
import test from 'node:test';
import { applyDefaultParts, applyProps } from '../scripts/lib/slots.mjs';

function component(html, parts = '') {
  return { id: 'slot-test', html, source: html, parts };
}

test('a render keeps a named part and strips its marker', () => {
  const html = applyProps(
    component('<div><span data-au-part="menu">M</span><span data-au-part="back">B</span></div>', 'menu back'),
    'parts=back');
  assert.match(html, />B</);
  assert.doesNotMatch(html, />M</);
  assert.doesNotMatch(html, /data-au-part/);
});

test('a quoted greater-than does not end the part tag', () => {
  const html = applyDefaultParts(
    component('<div><span title="a > b" data-au-part="keep">K</span></div>', 'keep'));
  assert.match(html, /title="a > b"/);
  assert.match(html, />K</);
  assert.doesNotMatch(html, /data-au-part/);
});

test('an unclosed part is rejected', () => {
  assert.throws(
    () => applyDefaultParts(component('<div><span data-au-part="menu">open', 'menu')),
    /Unclosed data-au-part 'menu'/);
});

test('nested parts of the same tag keep the inner when the outer is kept', () => {
  const html = applyProps(
    component('<div data-au-part="outer"><div data-au-part="inner">I</div></div>', 'outer inner'),
    'parts=outer inner');
  assert.equal(html, '<div><div>I</div></div>');
});

test('a long non-matching prefix does not hang the part scanner', () => {
  const poison = `<a${'0'.repeat(4000)}`;
  const started = Date.now();
  const html = applyProps(
    component(`<div>${poison}<span data-au-part="keep">K</span></div>`, 'keep'),
    'parts=keep');
  assert.ok(Date.now() - started < 500);
  assert.match(html, />K</);
  assert.doesNotMatch(html, /data-au-part/);
});
