import type { ForgeAccount, ForgeHosting, ForgeReview } from '@/features/git/models/ForgeHosting';
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
 * Which review a reviewing role supplies. A proposal needs one of each before it is settled, so
 * the two are slots rather than a list.
 */
export type ReviewSlotKind = 'dev' | 'pm';

/** What a reviewer left in their slot, if anything yet. */
export type ReviewMark = 'none' | 'approved' | 'warning' | 'blocked';

/** One of the two review slots on a proposal, and what fills it. */
export type ReviewSlot = { kind: ReviewSlotKind; mark: ReviewMark };

/**
 * How a proposal's checks stand as one signal. Failing beats running beats passing, because a
 * surface that reports a run in flight while something has already failed reads as healthier than
 * it is.
 */
export type ChecksState = 'none' | 'skipped' | 'running' | 'passed' | 'failed';

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
  checksSkipped: number;
  checksTotal: number;
  checks: ChecksState;
  verdicts: ForgeReview[];
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
  /** Which review this agent supplies for other agents' proposals. */
  reviews: ReviewSlotKind | null;
  /** The two slots on this agent's own proposal, empty when there is nothing to review. */
  slots: ReviewSlot[];
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
  checksSkipped: 0,
  checksTotal: 0,
  checks: 'none',
  verdicts: [],
};

export function countForge(hosting: ForgeHosting | null | undefined): ForgeStanding {
  if (!hosting) return noForge;
  const checks = hosting.checks ?? [];
  const threads = hosting.threads ?? [];
  const done = checks.filter(check => same(check.status, 'completed'));
  const passed = done.filter(check => same(check.conclusion, 'success')).length;
  const failed = done.filter(check => failedCheck(check.conclusion)).length;
  const running = checks.length - done.length;
  const skipped = done.length - passed - failed;
  return {
    availability: hosting.availability,
    proposalNumber: hosting.proposal?.number ?? null,
    proposalTitle: hosting.proposal?.title ?? null,
    proposalUrl: hosting.proposal?.url ?? null,
    checksPassed: passed,
    checksFailed: failed,
    checksRunning: running,
    unresolvedThreads: threads.filter(thread => !thread.isResolved).length,
    mergeState: hosting.mergeability?.state ?? null,
    detail: hosting.detail ?? null,
    checksSkipped: skipped,
    checksTotal: checks.length,
    checks: checksState(passed, failed, running, skipped),
    verdicts: hosting.reviews ?? [],
  };
}

function checksState(passed: number, failed: number, running: number, skipped: number): ChecksState {
  if (failed > 0) return 'failed';
  if (running > 0) return 'running';
  if (passed > 0) return 'passed';
  return skipped > 0 ? 'skipped' : 'none';
}

/**
 * A check that completed without running the thing it checks is neither a pass nor a failure, and
 * counting it as either misreports what the host said.
 */
function failedCheck(conclusion: string): boolean {
  return ['failure', 'timed_out', 'action_required', 'startup_failure']
    .some(kind => same(conclusion, kind));
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
    reviews: reviewKind(role),
    slots: [],
  };
}

/**
 * True when this agent works on a branch somebody else authored. A reviewer's workspace checks the
 * proposal out so it can be read, which puts a proposal on their branch that is not theirs: its
 * checks and its sign-offs are the author's standing, not the reviewer's.
 */
export function reviewsOthersWork(standing: TeamStanding): boolean {
  return standing.role === 'reviewer' || standing.reviews !== null;
}

/** Which review this role supplies, or null when it supplies none. */
export function reviewKind(role: AgentRole | null | undefined): ReviewSlotKind | null {
  const named = (role?.review ?? '').toLowerCase();
  return named === 'dev' ? 'dev' : named === 'pm' ? 'pm' : null;
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

/**
 * Where an agent stands when no unattended loop is running. The loop is the autonomous run, and an
 * agent can be working, held at a gate or stopped without one, so the session's own state is what
 * says which. A session merely existing is not a run in flight: an agent that has finished and is
 * sitting ready holds nothing.
 */
function withoutLoop(session: AgentSession | null): TeamStandingKind {
  if (same(session?.state, 'running')) return 'working';
  if (same(session?.state, 'awaiting_input')) return 'waiting';
  if (same(session?.state, 'error')) return 'halted';
  return 'idle';
}

function classify(session: AgentSession | null, loop: AgentSession['loop']): TeamStandingKind {
  if (!loop) return withoutLoop(session);
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
