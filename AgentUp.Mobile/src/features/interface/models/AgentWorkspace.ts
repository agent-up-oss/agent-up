import type { Workspace, WorkspaceApplication } from '@/features/workspaces/models/Workspace';
import type { TeamStanding } from './TeamStanding';

/** The sections an agent's own page is read in. */
export type AgentSection = 'overview' | 'chat' | 'git';

/** One fact about the work in hand: what is asked, and what answers it. */
export type TaskFact = { label: string; value: string };

/**
 * What the agent is working on, in the order the questions come: the task, what it has proposed
 * for it, and the branch and commit that proposal was built from.
 *
 * A fact the Server did not answer is left out rather than shown blank, because an empty row reads
 * as an answer of nothing rather than as a question nobody asked.
 */
export function taskFacts(standing: TeamStanding, workspace: Workspace | null): TaskFact[] {
  const facts: TaskFact[] = [];
  if (standing.taskNumber !== null)
    facts.push({ label: 'Task', value: `#${standing.taskNumber} · ${standing.taskTitle ?? ''}`.trim() });
  if (standing.forge.proposalNumber)
    facts.push({ label: 'Proposal', value: proposalLabel(standing) });
  const branch = workspace?.branch || standing.branch;
  if (branch) facts.push({ label: 'Branch', value: branch });
  if (workspace?.commit) facts.push({ label: 'Head', value: workspace.commit });
  return facts;
}

function proposalLabel(standing: TeamStanding): string {
  const number = `#${standing.forge.proposalNumber}`;
  const account = standing.identity?.login;
  return account ? `${number} · ${account}` : number;
}

/**
 * Where an application can be reached. A process that was given a port says which one and what
 * speaks there; one that was not says so, because a blank reads as a port nobody noticed.
 */
export function applicationAddress(application: WorkspaceApplication): string {
  const port = application.allocatedPorts?.[0];
  if (!port) return application.kind === 'Desktop' ? 'desktop · no port' : 'no port';
  return `:${port.allocatedPort} · ${port.protocol}`;
}

/**
 * How a proposal's review stands, counted rather than listed: a reader wants to know whether
 * anything is waiting on them before they want to know what each thread says.
 */
export function findingSummary(standing: TeamStanding): string {
  const threads = standing.forge.unresolvedThreads;
  if (!standing.forge.proposalNumber) return 'No proposal open.';
  if (threads === 0) return 'No unresolved threads.';
  return threads === 1 ? '1 unresolved thread' : `${threads} unresolved threads`;
}

/**
 * How the checks on a proposal stand, in one line. The counts are only worth reading once there is
 * a proposal to run them against.
 */
export function checkSummary(standing: TeamStanding): string {
  const forge = standing.forge;
  if (!forge.proposalNumber || forge.checksTotal === 0) return 'No checks reported.';
  const parts = [`${forge.checksPassed} passed`];
  if (forge.checksFailed > 0) parts.push(`${forge.checksFailed} failed`);
  if (forge.checksRunning > 0) parts.push(`${forge.checksRunning} running`);
  if (forge.checksSkipped > 0) parts.push(`${forge.checksSkipped} skipped`);
  return parts.join(' · ');
}

/**
 * What the agent is, under its name: the role it was given and the account it acts as. A workspace
 * without either is still an agent, so the line falls back to what its loop is doing.
 */
export function agentSubtitle(standing: TeamStanding): string {
  const parts: string[] = [];
  if (standing.role !== 'none') parts.push(roleName(standing.role));
  if (standing.identity) parts.push(`@${standing.identity.login}`);
  if (standing.agent) parts.push(standing.agent);
  return parts.length ? parts.join(' · ') : standing.state;
}

function roleName(role: TeamStanding['role']): string {
  if (role === 'developer') return 'Developer';
  if (role === 'reviewer') return 'Reviewer';
  return 'Agent';
}
