import assert from 'node:assert/strict';
import test from 'node:test';
import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { TeamMember } from '../models/TeamStanding';
import { readStanding, readStandings } from './TeamStandingProvider';

const server = { url: 'http://127.0.0.1:5000', token: null } as unknown as ServerSession;
const member: TeamMember = { workspaceId: 'shop', displayName: 'Bob', branch: 'main', state: 'Running' };

function respond(bodies: Record<string, unknown>): typeof fetch {
  return (async (input: RequestInfo | URL) => {
    const path = new URL(String(input)).pathname;
    const match = Object.keys(bodies).find(key => path.endsWith(key));
    if (!match) return new Response('not found', { status: 404 });
    return new Response(JSON.stringify(bodies[match]), {
      status: 200,
      headers: { 'content-type': 'application/json' },
    });
  }) as unknown as typeof fetch;
}

test('readStanding composes the agent session and the forge read', async () => {
  const standing = await readStanding(server, member, respond({
    '/agent': { workspaceId: 'shop', agent: 'Claude', state: 'ready', sessionId: null, error: null, agents: [], loop: { active: true, phase: 'running', taskNumber: 44, taskTitle: 'Restock' } },
    '/forge': { availability: 'ready', moduleId: 'github', proposal: { id: 'PR', number: '44', title: 'Restock', url: 'u', headRef: 'h', baseRef: 'main', state: 'open' }, checks: [], threads: [], mergeability: { mergeable: true, state: 'clean' }, detail: null },
  }));

  assert.equal(standing.kind, 'working');
  assert.equal(standing.taskNumber, 44);
  assert.equal(standing.forge.proposalNumber, '44');
  assert.equal(standing.forge.mergeState, 'clean');
});

test('readStanding keeps the agent when the forge read fails', async () => {
  const standing = await readStanding(server, member, respond({
    '/agent': { workspaceId: 'shop', agent: 'Claude', state: 'ready', sessionId: null, error: null, agents: [] },
  }));

  assert.equal(standing.agent, 'Claude');
  assert.equal(standing.forge.availability, 'none');
});

test('readStanding still stands when neither read answers', async () => {
  const standing = await readStanding(server, member, respond({}));

  assert.equal(standing.kind, 'idle');
  assert.equal(standing.agent, null);
  assert.equal(standing.displayName, 'Bob');
});

test('readStandings reads every member and keeps their order', async () => {
  const members: TeamMember[] = [
    member,
    { workspaceId: 'docs', displayName: 'Hector', branch: 'fix/51', state: 'Failed' },
  ];
  const standings = await readStandings(server, members, respond({}));

  assert.deepEqual(standings.map(item => item.workspaceId), ['shop', 'docs']);
});
