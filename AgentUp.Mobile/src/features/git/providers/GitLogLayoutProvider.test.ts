import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  formatGitLogTime,
  gitLogGraphEdges,
  gitLogLaneX,
  layoutGitLog,
} from './GitLogLayoutProvider';

const root = { id: 'aaa', shortId: 'aaa', parents: [], subject: 'root', author: 'A', timestamp: '2026-01-01T00:00:00Z', refs: ['main'] };
const child = { id: 'bbb', shortId: 'bbb', parents: ['aaa'], subject: 'child', author: 'A', timestamp: '2026-01-02T00:00:00Z', refs: ['HEAD', 'main'] };
const merge = { id: 'ccc', shortId: 'ccc', parents: ['bbb', 'aaa'], subject: 'merge', author: 'A', timestamp: '2026-01-03T00:00:00Z', refs: ['HEAD', 'main'] };

test('layoutGitLog keeps the first parent on the same lane', () => {
  const rows = layoutGitLog([child, root]);

  assert.equal(rows.length, 2);
  assert.equal(rows[0].lane, 0);
  assert.equal(rows[1].lane, 0);
  assert.equal(rows[0].checkoutName, 'main');
  assert.deepEqual(rows[0].refs.map(ref => ref.kind), ['head', 'local']);
  assert.deepEqual(rows[0].outgoing, [{ fromLane: 0, toLane: 0, colorLane: 0 }]);
  assert.deepEqual(rows[1].incomingLanes, [0]);
});

test('layoutGitLog assigns a new lane to a second parent and records a merge curve', () => {
  const rows = layoutGitLog([merge, child, root]);

  assert.equal(rows[0].lane, 0);
  assert.deepEqual(rows[0].parentLanes, [0, 1]);
  assert.ok(rows[0].outgoing.some(link => link.fromLane === 0 && link.toLane === 1));
  assert.equal(rows[0].laneCount, 2);
  assert.deepEqual(rows[1].incomingLanes, [0, 1]);
  assert.ok(rows[1].outgoing.some(link => link.fromLane === 1 && link.toLane === 1));
});

test('gitLogGraphEdges draws verticals through a row and a cubic for a merge', () => {
  const rows = layoutGitLog([merge, child, root]);
  const edges = gitLogGraphEdges(rows[0]);
  const mergeEdge = edges.find(edge => edge.d.includes('C'));
  assert.ok(mergeEdge);
  assert.equal(mergeEdge.colorLane, 1);
  assert.match(mergeEdge.d, new RegExp(`M ${gitLogLaneX(0)} 14 C ${gitLogLaneX(0)} 21, ${gitLogLaneX(1)} 21, ${gitLogLaneX(1)} 28`));
  assert.ok(edges.some(edge => edge.d === `M ${gitLogLaneX(0)} 14 L ${gitLogLaneX(0)} 28`));
});

test('formatGitLogTime uses relative and calendar buckets', () => {
  const now = new Date(2026, 0, 3, 12, 0, 0).getTime();
  assert.equal(formatGitLogTime(new Date(2026, 0, 3, 11, 36, 0).toISOString(), now), '24 minutes ago');
  assert.equal(formatGitLogTime(new Date(2026, 0, 3, 8, 5, 0).toISOString(), now), 'Today 08:05');
  assert.equal(formatGitLogTime(new Date(2026, 0, 2, 21, 5, 0).toISOString(), now), 'Yesterday 21:05');
});
