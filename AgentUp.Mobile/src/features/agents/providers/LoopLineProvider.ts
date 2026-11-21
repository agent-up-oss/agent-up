import type { AgentDescriptor, AgentId, AgentLoopStatus, ServerAutonomy } from '@agent-up/chat';

export function formatLoopLine(loop: AgentLoopStatus | null | undefined): string | null {
  if (!loop) return null;
  const parts = [loop.phase, loop.taskTitle, loop.terminal, loop.stopReason]
    .filter((part): part is string => typeof part === 'string' && part.length > 0);
  return parts.length === 0 ? null : parts.join(' · ');
}

export function boundAgentActionLabel(
  boundAgent: AgentId | null | undefined,
  currentAgent: AgentId | null | undefined,
  agents: AgentDescriptor[] | undefined,
): string | null {
  if (!boundAgent) return null;
  const displayName = agents?.find(item => item.agent.toLowerCase() === boundAgent.toLowerCase())?.displayName
    ?? boundAgent;
  const current = currentAgent?.toLowerCase() === boundAgent.toLowerCase();
  return current ? `Open ${displayName}` : `Start ${displayName}`;
}

/**
 * The one line Agents shows for the unattended loop, whose checklist and actions live in the
 * workspace Settings tab. Null when the Server reports no autonomy chain.
 */
export function loopSettingsSummary(autonomy: ServerAutonomy | null | undefined): string | null {
  const items = autonomy?.preconditions ?? [];
  if (items.length === 0) return null;
  const open = items.filter(item => !item.satisfied).length;
  return open === 0 ? 'Unattended loop ready' : `Unattended loop · ${open} of ${items.length} checks open`;
}
