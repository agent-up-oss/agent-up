import type { ForgeHosting } from '@/features/git/models/ForgeHosting';
import type { AgentSession } from '@agent-up/chat';

/**
 * What one agent needs from you. The bridge groups by this and nothing else, so a surface never
 * has to decide for itself which of two reads wins.
 */
export type TeamStandingKind = 'idle' | 'working' | 'waiting' | 'halted';

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
  return {
    ...member,
    agent: session?.agent ?? session?.boundAgent ?? null,
    kind: classify(session, loop),
    taskNumber: loop?.taskNumber ?? null,
    taskTitle: loop?.taskTitle ?? null,
    reason: loop?.blockReason || forge.detail || loop?.stopReason || null,
    diagnosticCode: loop?.stopReason ?? null,
    permissionRuleIds: loop?.permissionRuleIds ?? [],
    forge,
  };
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
