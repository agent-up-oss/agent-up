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

test('each review slot is filled by the teammate who supplies that review', async () => {
  const team = [
    { workspaceId: 'bob', displayName: 'Bob', branch: 'feat/44', state: 'Running' },
    { workspaceId: 'hector', displayName: 'Hector', branch: 'main', state: 'Running' },
    { workspaceId: 'lucy', displayName: 'Lucy', branch: 'main', state: 'Running' },
  ];
  const standings = await readStandings(server, team, byWorkspace({
    bob: {
      agent: {},
      forge: forgeWith({ login: 'bob-aup[bot]' }, [
        { login: 'hector-aup[bot]', state: 'APPROVED' },
        { login: 'lucy-aup[bot]', state: 'COMMENTED' },
      ]),
    },
    hector: { agent: reviewerSession('Dev'), forge: forgeWith({ login: 'hector-aup[bot]' }, []) },
    lucy: { agent: reviewerSession('Pm'), forge: forgeWith({ login: 'lucy-aup[bot]' }, []) },
  }));

  const slots = standings.find(item => item.workspaceId === 'bob')!.slots;
  assert.deepEqual(slots.map(slot => slot.kind), ['dev', 'pm']);
  assert.equal(slots[0].mark, 'approved');
  assert.equal(slots[1].mark, 'warning');
});

test('a verdict from outside the team fills no slot, and no proposal means no slots', async () => {
  const team = [
    { workspaceId: 'bob', displayName: 'Bob', branch: 'feat/44', state: 'Running' },
    { workspaceId: 'hector', displayName: 'Hector', branch: 'main', state: 'Running' },
  ];
  const standings = await readStandings(server, team, byWorkspace({
    bob: { agent: {}, forge: forgeWith({ login: 'bob-aup[bot]' }, [{ login: 'stranger', state: 'APPROVED' }]) },
    hector: { agent: reviewerSession('Dev'), forge: { availability: 'noProposal', proposal: null, checks: [], threads: [], mergeability: null, detail: null, moduleId: 'github', account: null, reviews: [] } },
  }));

  assert.deepEqual(standings.find(item => item.workspaceId === 'bob')!.slots.map(slot => slot.mark), ['none', 'none']);
  assert.deepEqual(standings.find(item => item.workspaceId === 'hector')!.slots, []);
});

function reviewerSession(review: string) {
  return { role: { prompt: 'p', tools: [], trigger: 'ProposalAwaitingReview', termination: 'ReviewSubmitted', review } };
}

function forgeWith(account: { login: string }, reviews: Array<{ login: string; state: string }>) {
  return {
    availability: 'ready',
    moduleId: 'github',
    proposal: { id: 'PR', number: '44', title: 'Restock', url: 'u', headRef: 'h', baseRef: 'main', state: 'open' },
    checks: [],
    threads: [],
    mergeability: { mergeable: true, state: 'clean' },
    detail: null,
    account: { ...account, displayName: account.login, isBot: true, url: 'u' },
    reviews,
  };
}

/** Answers each workspace's two routes from one table, so a test states the whole team at once. */
function byWorkspace(table: Record<string, { agent: object; forge: object }>): typeof fetch {
  return (async (input: RequestInfo | URL) => {
    const path = new URL(String(input)).pathname;
    const id = Object.keys(table).find(key => path.includes(`/workspaces/${key}/`));
    if (!id) return new Response('not found', { status: 404 });
    const body = path.endsWith('/forge')
      ? table[id].forge
      : { workspaceId: id, agent: null, state: 'ready', sessionId: null, error: null, agents: [], ...table[id].agent };
    return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } });
  }) as unknown as typeof fetch;
}
