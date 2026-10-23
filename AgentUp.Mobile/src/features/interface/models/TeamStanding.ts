import type { ForgeAccount, ForgeHosting } from '@/features/git/models/ForgeHosting';
import type { AgentRole, AgentSession } from '@agent-up/chat';

/**
 * What one agent needs from you. The bridge groups by this and nothing else, so a surface never
 * has to decide for itself which of two reads wins.
 */
export type TeamStandingKind = 'idle' | 'working' | 'waiting' | 'halted';

/**
 * What an agent is for, as the Server's role trigger and termination say. A role that pairs them
 * any other way is named from its two halves rather than forced into one of these.
 */
export type TeamRoleKind = 'none' | 'developer' | 'reviewer' | 'other';

/**
 * The account a workspace acts as on its repository host. One workspace keeps one account, so
 * everything its agent pushes, comments, or reviews is attributable to this name.
 */
export type TeamIdentity = {
  login: string;
  displayName: string;
  isBot: boolean;
  url: string;
};

/** The workspace a standing is read for: only the fields a triage row shows. */
export type TeamMember = {
  workspaceId: string;
  displayName: string;
  branch: string;
  state: string;
};

/** What the repository host reports for one workspace, counted. */
export type ForgeStanding = {
  availability: string;
  proposalNumber: string | null;
  proposalTitle: string | null;
  proposalUrl: string | null;
  checksPassed: number;
  checksFailed: number;
  checksRunning: number;
  unresolvedThreads: number;
  mergeState: string | null;
  detail: string | null;
};

/**
 * Where one agent stands, composed from the two reads a Server answers per workspace: its agent
 * session and its forge. Nothing here is invented — a field is null when the Server said nothing.
 */
export type TeamStanding = TeamMember & {
  agent: string | null;
  kind: TeamStandingKind;
  taskNumber: number | null;
  taskTitle: string | null;
  reason: string | null;
  diagnosticCode: string | null;
  permissionRuleIds: string[];
  forge: ForgeStanding;
  role: TeamRoleKind;
  roleTools: string[];
  identity: TeamIdentity | null;
};

export const noForge: ForgeStanding = {
  availability: 'none',
  proposalNumber: null,
  proposalTitle: null,
  proposalUrl: null,
  checksPassed: 0,
  checksFailed: 0,
  checksRunning: 0,
  unresolvedThreads: 0,
  mergeState: null,
  detail: null,
};

export function countForge(hosting: ForgeHosting | null | undefined): ForgeStanding {
  if (!hosting) return noForge;
  const checks = hosting.checks ?? [];
  const threads = hosting.threads ?? [];
  return {
    availability: hosting.availability,
    proposalNumber: hosting.proposal?.number ?? null,
    proposalTitle: hosting.proposal?.title ?? null,
    proposalUrl: hosting.proposal?.url ?? null,
    checksPassed: checks.filter(check => same(check.conclusion, 'success')).length,
    checksFailed: checks.filter(check => same(check.conclusion, 'failure')).length,
    checksRunning: checks.filter(check => !same(check.status, 'completed')).length,
    unresolvedThreads: threads.filter(thread => !thread.isResolved).length,
    mergeState: hosting.mergeability?.state ?? null,
    detail: hosting.detail ?? null,
  };
}

export function composeStanding(
  member: TeamMember,
  session: AgentSession | null,
  hosting: ForgeHosting | null,
): TeamStanding {
  const loop = session?.loop ?? null;
  const forge = countForge(hosting);
  const role = session?.role ?? null;
  return {
    ...member,
    agent: agentName(session),
    kind: classify(session, loop),
    taskNumber: loop?.taskNumber ?? null,
    taskTitle: loop?.taskTitle ?? null,
    reason: loop?.blockReason || forge.detail || loop?.stopReason || null,
    diagnosticCode: loop?.stopReason ?? null,
    permissionRuleIds: loop?.permissionRuleIds ?? [],
    forge,
    role: classifyRole(role),
    roleTools: role?.tools ?? [],
    identity: identify(hosting?.account),
  };
}

/**
 * The agent driving this workspace, named the way its capability names itself. The session carries
 * the module id, which is lowercase and not what anyone calls the thing.
 */
function agentName(session: AgentSession | null): string | null {
  const id = session?.agent ?? session?.boundAgent ?? null;
  if (!id) return null;
  const descriptor = session?.agents?.find(item => item.agent.toLowerCase() === id.toLowerCase());
  return descriptor?.displayName || id;
}

/**
 * What the role is for, from the pair the Server stores. An issue in and a proposal out is a
 * builder; a proposal in and a review out is a reviewer. Any other pairing is a role someone
 * configured deliberately, so it is reported as its own thing rather than squeezed into one.
 */
export function classifyRole(role: AgentRole | null | undefined): TeamRoleKind {
  if (!role) return 'none';
  if (role.trigger === 'IssueAssigned' && role.termination === 'ProposalOpened') return 'developer';
  if (role.trigger === 'ProposalAwaitingReview' && role.termination === 'ReviewSubmitted') return 'reviewer';
  return 'other';
}

function identify(account: ForgeAccount | null | undefined): TeamIdentity | null {
  return account
    ? { login: account.login, displayName: account.displayName, isBot: account.isBot, url: account.url }
    : null;
}

function classify(session: AgentSession | null, loop: AgentSession['loop']): TeamStandingKind {
  if (!loop) return session ? 'working' : 'idle';
  if (isHalted(loop)) return 'halted';
  if (isWaiting(session, loop)) return 'waiting';
  return loop.active ? 'working' : 'idle';
}

function isHalted(loop: NonNullable<AgentSession['loop']>): boolean {
  if (same(loop.phase, 'failed') || same(loop.terminal, 'Failed')) return true;
  return !loop.active && !!loop.stopReason && !same(loop.stopReason, 'queue_empty');
}

function isWaiting(session: AgentSession | null, loop: NonNullable<AgentSession['loop']>): boolean {
  return same(loop.phase, 'blocked')
    || same(loop.terminal, 'BlockedAwaitingInput')
    || !!loop.blockReason
    || same(session?.state, 'awaiting_input');
}

function same(left: string | null | undefined, right: string): boolean {
  return (left ?? '').toLowerCase() === right.toLowerCase();
}
