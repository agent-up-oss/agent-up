import { getForgeHosting } from '@/features/git/providers/GitApiProvider';
import { requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { AgentSession } from '@agent-up/chat';
import { composeStanding, type TeamMember, type TeamStanding } from '../models/TeamStanding';

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
  return Promise.all(members.map(member => readStanding(server, member, request)));
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
