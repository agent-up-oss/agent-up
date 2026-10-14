import type { AgentLoopStatus } from '@agent-up/chat';

export function formatLoopLine(loop: AgentLoopStatus | null | undefined): string | null {
  if (!loop) return null;
  const parts = [loop.phase, loop.taskTitle, loop.terminal, loop.stopReason]
    .filter((part): part is string => typeof part === 'string' && part.length > 0);
  return parts.length === 0 ? null : parts.join(' · ');
}
