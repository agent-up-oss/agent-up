import { reviewsOthersWork, type ReviewSlot, type TeamStanding, type TeamStandingKind } from '../models/TeamStanding';

/** The bridge's three groups, plus the queue, from one list of standings. */
export type TeamTriage = {
  /** Everyone. The rail draws from this; the bridge shows each agent once instead. */
  team: TeamStanding[];
  waiting: TeamStanding[];
  halted: TeamStanding[];
  /** The rest of the team: running or idle, and needing nothing from you. */
  settled: TeamStanding[];
  tasks: TaskGroup[];
  headline: string;
};

/**
 * One task in the queue, with everyone on it. A task is not one agent's: an author builds it and
 * the reviewers read what they built, so an issue with three agents on it is one row naming three
 * people, not three rows naming the same issue.
 */
export type TaskGroup = {
  number: number;
  label: string;
  kind: TeamStandingKind;
  agents: TeamStanding[];
};

export function triage(standings: TeamStanding[]): TeamTriage {
  const waiting = standings.filter(standing => standing.kind === 'waiting');
  const halted = standings.filter(standing => standing.kind === 'halted');
  return {
    team: standings,
    waiting,
    halted,
    settled: standings.filter(standing => standing.kind !== 'waiting' && standing.kind !== 'halted'),
    tasks: queue(standings),
    headline: `${standings.length} ${plural(standings.length, 'agent')} · ${waiting.length} waiting on you · ${halted.length} halted`,
  };
}

/**
 * The queue: one row per task, most urgent first, with every agent on that task. An issue three
 * agents hold is one piece of work, not three.
 */
export function queue(standings: TeamStanding[]): TaskGroup[] {
  const byTask = new Map<number, TeamStanding[]>();
  for (const standing of standings) {
    if (standing.taskNumber === null) continue;
    const held = byTask.get(standing.taskNumber) ?? [];
    held.push(standing);
    byTask.set(standing.taskNumber, held);
  }
  return [...byTask.entries()]
    .map(([number, agents]) => ({ number, label: taskGroupLabel(number, agents), kind: taskKind(agents), agents }))
    .sort((left, right) => urgency(left.kind) - urgency(right.kind) || left.number - right.number);
}

function taskGroupLabel(number: number, agents: TeamStanding[]): string {
  const title = agents.map(agent => agent.taskTitle).find(name => !!name);
  return title ? `#${number} · ${title}` : `#${number}`;
}

/**
 * Where the task stands, which is the most urgent thing any of its agents needs. A task with one
 * agent at a gate needs you, whatever the others are doing.
 */
function taskKind(agents: TeamStanding[]): TeamStandingKind {
  return [...agents].sort((left, right) => urgency(left.kind) - urgency(right.kind))[0]?.kind ?? 'idle';
}

/** How soon a standing needs somebody; lower comes first in the queue. */
function urgency(kind: TeamStandingKind): number {
  if (kind === 'waiting') return 0;
  if (kind === 'halted') return 1;
  return kind === 'working' ? 2 : 3;
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

/**
 * What this agent is doing, in their own terms. A reviewer never works on an issue — the branch is
 * somebody else's and the only thing they produce is a review — so saying they are "working on" it
 * would claim work that is not theirs.
 */
export function headlineFor(standing: TeamStanding): string {
  const reviews = reviewsOthersWork(standing);
  const task = standing.taskNumber === null ? '' : `${reviews ? ' ' : ' on '}#${standing.taskNumber}`;
  const verb = reviews ? 'reviewing' : 'working';
  if (standing.kind === 'waiting') {
    return `${standing.displayName} is waiting on you${standing.taskNumber === null ? '' : ` on #${standing.taskNumber}`}`;
  }
  if (standing.kind === 'halted') return `${standing.displayName} stopped ${verb}${task}`;
  if (standing.kind === 'working') return `${standing.displayName} is ${verb}${task}`;
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

/**
 * True while this agent holds a proposal of its own. A reviewer has one on its branch too, but it
 * belongs to whoever opened it, so the row would report somebody else's standing.
 */
export function hasStatusRow(standing: TeamStanding): boolean {
  return !!standing.forge.proposalNumber && !reviewsOthersWork(standing);
}
