import assert from 'node:assert/strict';
import { test } from 'node:test';
import { layoutGitLog } from './GitLogLayoutProvider';

test('layoutGitLog keeps the first parent on the same lane', () => {
  const rows = layoutGitLog([
    { id: 'bbb', shortId: 'bbb', parents: ['aaa'], subject: 'child', author: 'A', timestamp: '2026-01-02T00:00:00Z', refs: ['HEAD', 'main'] },
    { id: 'aaa', shortId: 'aaa', parents: [], subject: 'root', author: 'A', timestamp: '2026-01-01T00:00:00Z', refs: ['main'] },
  ]);

  assert.equal(rows.length, 2);
  assert.equal(rows[0].lane, 0);
  assert.equal(rows[1].lane, 0);
  assert.equal(rows[0].checkoutName, 'main');
  assert.match(rows[0].graph, /\*/);
});

test('layoutGitLog assigns a new lane to a second parent', () => {
  const rows = layoutGitLog([
    { id: 'ccc', shortId: 'ccc', parents: ['bbb', 'aaa'], subject: 'merge', author: 'A', timestamp: '2026-01-03T00:00:00Z', refs: ['main'] },
    { id: 'bbb', shortId: 'bbb', parents: ['aaa'], subject: 'child', author: 'A', timestamp: '2026-01-02T00:00:00Z', refs: [] },
    { id: 'aaa', shortId: 'aaa', parents: [], subject: 'root', author: 'A', timestamp: '2026-01-01T00:00:00Z', refs: [] },
  ]);

  assert.equal(rows[0].lane, 0);
  assert.deepEqual(rows[0].parentLanes, [0, 1]);
});
