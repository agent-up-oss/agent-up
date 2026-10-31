import { getForgeHosting } from '@/features/git/providers/GitApiProvider';
import { requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { AgentSession } from '@agent-up/chat';
import {
  composeStanding,
  type ReviewMark,
  type ReviewSlot,
  type ReviewSlotKind,
  type TeamMember,
  type TeamStanding,
} from '../models/TeamStanding';

/**
 * Reads one agent's standing from the two routes a Server answers for a workspace. A read that
 * fails leaves that side null rather than failing the whole bridge: one unreachable agent must not
 * hide the rest of the team.
 */
export async function readStanding(
  server: ServerSession,
  member: TeamMember,
  request: typeof fetch = fetch,
): Promise<TeamStanding> {
  const [session, hosting] = await Promise.all([
    orNull(() => readAgentSession(server, member.workspaceId, request)),
    orNull(() => getForgeHosting(server, member.workspaceId, request)),
  ]);
  return composeStanding(member, session, hosting);
}

export async function readStandings(
  server: ServerSession,
  members: TeamMember[],
  request: typeof fetch = fetch,
): Promise<TeamStanding[]> {
  const standings = await Promise.all(members.map(member => readStanding(server, member, request)));
  return standings.map(standing => ({ ...standing, slots: slotsFor(standing, standings) }));
}

/**
 * Fills the two review slots on one agent's proposal. A verdict names the account that left it,
 * and every account on this Server belongs to a workspace whose role says which review it supplies,
 * so the team is what turns a list of verdicts into slots. A verdict from an account outside the
 * team fills no slot: it is somebody's review, but not one of these two.
 */
function slotsFor(standing: TeamStanding, team: TeamStanding[]): ReviewSlot[] {
  if (!standing.forge.proposalNumber) return [];
  return (['dev', 'pm'] as ReviewSlotKind[]).map(kind => ({ kind, mark: markFor(kind, standing, team) }));
}

function markFor(kind: ReviewSlotKind, standing: TeamStanding, team: TeamStanding[]): ReviewMark {
  const verdict = standing.forge.verdicts.find(item => supplies(kind, item.login, team));
  return verdict ? mark(verdict.state) : 'none';
}

function supplies(kind: ReviewSlotKind, login: string, team: TeamStanding[]): boolean {
  return team.some(member =>
    member.reviews === kind
    && !!member.identity
    && member.identity.login.toLowerCase() === login.toLowerCase());
}

/**
 * What a verdict means for its slot. The three a host distinguishes are the three that matter:
 * approved settles the slot, changes requested blocks it, and a plain comment is worth reading
 * without standing in the way. Anything else has not been said yet.
 */
function mark(state: string): ReviewMark {
  const said = state.toUpperCase();
  if (said === 'APPROVED') return 'approved';
  if (said === 'CHANGES_REQUESTED') return 'blocked';
  if (said === 'COMMENTED') return 'warning';
  return 'none';
}

/**
 * The agent session, read straight off the route. The chat package's own reader would pull React
 * Native in behind it, which this slice does not need and a test cannot load.
 */
function readAgentSession(
  server: ServerSession,
  workspaceId: string,
  request: typeof fetch,
): Promise<AgentSession | null> {
  return requestServerJson<AgentSession | null>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/agent`,
    { method: 'GET' },
    undefined,
    request,
  );
}

async function orNull<T>(read: () => Promise<T>): Promise<T | null> {
  try {
    return await read();
  } catch {
    return null;
  }
}
