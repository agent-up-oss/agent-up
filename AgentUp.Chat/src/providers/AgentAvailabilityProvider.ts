import type { AgentDescriptor } from '../models/AgentSession';

export const capabilityNotEnabled = 'capabilityNotEnabled';
export const executableNotFound = 'executableNotFound';

export type AgentRowPresentation = {
  status: string;
  detail: string | null;
  remedy: string | null;
  canSelect: boolean;
  canEnable: boolean;
  isBlocked: boolean;
};

export function presentAgentRow(agent: AgentDescriptor): AgentRowPresentation {
  const canSelect = agent.available;
  const canEnable = !agent.available && agent.unavailability === capabilityNotEnabled;
  return {
    status: canSelect ? 'Available' : canEnable ? 'Not enabled' : 'Executable not found',
    detail: agent.detail ?? null,
    remedy: agent.remedy ?? null,
    canSelect,
    canEnable,
    isBlocked: !canSelect && !canEnable,
  };
}

export function agentRowDetail(agent: AgentDescriptor): string {
  const row = presentAgentRow(agent);
  if (row.canSelect) return 'Create a new session';
  return [row.detail, row.remedy].filter(part => typeof part === 'string' && part.length > 0).join(' ');
}
