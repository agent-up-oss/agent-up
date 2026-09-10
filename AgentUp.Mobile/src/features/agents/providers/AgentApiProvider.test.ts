import assert from 'node:assert/strict';
import test from 'node:test';
import { parseSseFrames } from './AgentApiProvider';

test('parses fragmented and multiline SSE events without losing the tail', () => {
  const first = parseSseFrames('id: 1\nevent: state\ndata: {"sequence":1,\n');
  assert.equal(first.events.length, 0);
  const second = parseSseFrames(first.rest + 'data: "type":"state","payload":{},"timestamp":"now"}\n\npartial');
  assert.equal(second.events[0]?.sequence, 1);
  assert.equal(second.rest, 'partial');
});

test('ignores keepalives and malformed data frames', () => {
  const parsed = parseSseFrames(': keepalive\n\ndata: nope\n\n');
  assert.deepEqual(parsed.events, []);
});
