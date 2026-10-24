import type { AgentLoopStatus, ServerAutonomy } from '../models/AgentSession';

export function preconditionLabel(id: string): string {
  switch (id) {
    case 'forgeCapabilityEnabled':
      return 'Forge capability';
    case 'hostConnectionAuthorized':
      return 'Host connection';
    case 'agentAvailable':
      return 'Agent available';
    case 'autonomyEnabled':
      return 'Autonomy enabled';
    default:
      return id;
  }
}

export function canEnableAutonomy(autonomy: ServerAutonomy | null | undefined): boolean {
  const items = autonomy?.preconditions ?? [];
  return items.some(item => item.id === 'forgeCapabilityEnabled' && item.satisfied)
    && items.some(item => item.id === 'hostConnectionAuthorized' && item.satisfied)
    && items.some(item => item.id === 'agentAvailable' && item.satisfied)
    && items.some(item => item.id === 'autonomyEnabled' && !item.satisfied);
}

export function canActivateLoop(
  autonomy: ServerAutonomy | null | undefined,
  loop: AgentLoopStatus | null | undefined,
): boolean {
  const items = autonomy?.preconditions ?? [];
  return items.length > 0 && items.every(item => item.satisfied) && loop?.active !== true;
}

export function canCancelLoop(loop: AgentLoopStatus | null | undefined): boolean {
  return loop?.active === true;
}

/** Sign-in owns the chat chrome; the loop checklist would cover the paste-code field. */
export function shouldShowLoopControls(state: string | null | undefined): boolean {
  return state !== 'authentication_required' && state !== 'authenticating';
}
