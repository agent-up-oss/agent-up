import type { AgentDescriptor, AgentLoopStatus, ServerAutonomy } from '@agent-up/chat/models/AgentSession';
import { presentAgentRow } from '@agent-up/chat/providers/AgentAvailabilityProvider';
import type { SceneSlotItem } from './CommandCenterSceneProvider';

/** What this member's Settings tab knows about the agent and loop. */
export type ChatSettingsInput = {
  autonomy: ServerAutonomy | null;
  loop: AgentLoopStatus | null | undefined;
  agents: AgentDescriptor[];
  boundAgent: string | null | undefined;
};

type Field = {
  id: string;
  title: string;
  detail: string;
  actionLabel: string;
  action: string | null;
  enabled: boolean;
  children?: Field[];
};

/**
 * The autonomous toggle and the agents this member can run. Locked precondition
 * rows stay off the page; those defaults live on the Server.
 */
export function chatSettingsFields(input: ChatSettingsInput): SceneSlotItem[] {
  const on = input.loop?.active === true;
  const autonomy: Field = {
    id: 'autonomy',
    title: 'Autonomous',
    detail: on ? 'On' : 'Off',
    actionLabel: on ? 'Disable' : 'Enable',
    action: on ? 'cancel-loop' : 'toggle-autonomy',
    enabled: true,
  };
  const agents = input.agents.map(agent => agentField(agent, input.boundAgent));
  return [autonomy, ...agents].map(slot);
}

/** The team as tabs; unused once member settings live on that member's page. */
export function settingsWorkspaces(team: Array<{ workspaceId: string; displayName: string }>, selected: string | null): SceneSlotItem[] {
  return team.map(member => ({
    id: member.workspaceId,
    action: 'settings-workspace',
    state: member.workspaceId === selected ? 'au-app-tab--selected' : undefined,
    text: { name: member.displayName },
  }));
}

function agentField(agent: AgentDescriptor, boundAgent: string | null | undefined): Field {
  const row = presentAgentRow(agent);
  const bound = agent.agent === boundAgent;
  if (row.canSelect) {
    return { id: agent.agent, title: agent.displayName, detail: bound ? 'Bound' : row.status, actionLabel: bound ? 'Bound' : 'Use', action: 'select-agent', enabled: !bound };
  }
  const detail = row.remedy ?? row.status;
  return { id: agent.agent, title: agent.displayName, detail, actionLabel: 'Enable', action: 'enable-agent', enabled: row.canEnable };
}

function slot(field: Field): SceneSlotItem {
  const parts = ['control'];
  if (!field.enabled) parts.push('locked');
  return {
    id: field.id,
    action: field.enabled && field.action ? field.action : undefined,
    parts,
    text: { title: field.title, detail: field.detail, action: field.actionLabel },
    items: field.children?.length ? { fields: field.children.map(slot) } : undefined,
  };
}
