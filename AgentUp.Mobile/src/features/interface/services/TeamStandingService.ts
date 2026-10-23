import type { ReviewSlot, TeamStanding, TeamStandingKind } from '../models/TeamStanding';

/** The bridge's three groups, plus the queue, from one list of standings. */
export type TeamTriage = {
  /** Everyone. The rail draws from this; the bridge shows each agent once instead. */
  team: TeamStanding[];
  waiting: TeamStanding[];
  halted: TeamStanding[];
  /** The rest of the team: running or idle, and needing nothing from you. */
  settled: TeamStanding[];
  tasks: TeamStanding[];
  headline: string;
};

export function triage(standings: TeamStanding[]): TeamTriage {
  const waiting = standings.filter(standing => standing.kind === 'waiting');
  const halted = standings.filter(standing => standing.kind === 'halted');
  return {
    team: standings,
    waiting,
    halted,
    settled: standings.filter(standing => standing.kind !== 'waiting' && standing.kind !== 'halted'),
    tasks: standings.filter(standing => standing.taskNumber !== null),
    headline: `${standings.length} ${plural(standings.length, 'agent')} · ${waiting.length} waiting on you · ${halted.length} halted`,
  };
}

export function stateLabel(kind: TeamStandingKind): string {
  if (kind === 'waiting') return '◆ gate open';
  if (kind === 'halted') return '▲ halted';
  if (kind === 'working') return '● working';
  return '○ idle';
}

/** What this agent is for, in one word, or null while no role is configured. */
export function roleLabel(standing: TeamStanding): string | null {
  if (standing.role === 'developer') return 'Developer';
  if (standing.role === 'reviewer') return 'Reviewer';
  if (standing.role === 'other') return 'Custom role';
  return null;
}

/** The account as a host writes it, which is how a reviewer will see it on a thread. */
export function identityHandle(standing: TeamStanding): string | null {
  if (!standing.identity) return null;
  return standing.identity.login.startsWith('@') ? standing.identity.login : `@${standing.identity.login}`;
}

/**
 * Who this agent is, under their name: what they are for, which account they act as, and which
 * model drives them. Each part is dropped when the Server did not report it, so the line never
 * pads itself out with placeholders.
 */
export function whoIs(standing: TeamStanding): string {
  return [roleLabel(standing), identityHandle(standing), standing.agent, standing.state]
    .filter((part): part is string => !!part)
    .join(' · ');
}

export function taskLabel(standing: TeamStanding): string | null {
  if (standing.taskNumber === null) return null;
  return standing.taskTitle ? `#${standing.taskNumber} · ${standing.taskTitle}` : `#${standing.taskNumber}`;
}

export function headlineFor(standing: TeamStanding): string {
  const suffix = standing.taskNumber === null ? '' : ` on #${standing.taskNumber}`;
  if (standing.kind === 'waiting') return `${standing.displayName} is waiting on you${suffix}`;
  if (standing.kind === 'halted') return `${standing.displayName} stopped${suffix}`;
  if (standing.kind === 'working') return `${standing.displayName} is working${suffix}`;
  return `${standing.displayName} has nothing in hand`;
}

/**
 * Whether the proposal is worth naming next to the task. A proposal opened for the task it is
 * listed under repeats it word for word; one opened under a different number does not.
 */
export function showProposalBesideTask(standing: TeamStanding): boolean {
  if (!standing.forge.proposalNumber) return false;
  return standing.forge.proposalNumber !== String(standing.taskNumber ?? '');
}

export function threadSummary(standing: TeamStanding): string {
  const count = standing.forge.unresolvedThreads;
  return count === 1 ? '1 unresolved review thread' : `${count} unresolved review threads`;
}

function plural(count: number, word: string): string {
  return count === 1 ? word : `${word}s`;
}

/**
 * How the checks stand, as "(passed/total)". The dot beside it carries the verdict, so the numbers
 * only have to say how much of the run is accounted for.
 */
export function checksCount(standing: TeamStanding): string {
  return `(${standing.forge.checksPassed}/${standing.forge.checksTotal})`;
}

/** One review slot as it is drawn: a mark, or a dash while nobody has filled it. */
export function slotGlyph(slot: ReviewSlot): string {
  if (slot.mark === 'approved') return '\u2713';
  if (slot.mark === 'warning') return '!';
  if (slot.mark === 'blocked') return '\u2717';
  return '\u2013';
}

/** True while this agent has a proposal, which is what the status row reports on. */
export function hasStatusRow(standing: TeamStanding): boolean {
  return !!standing.forge.proposalNumber;
}
