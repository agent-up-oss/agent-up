import type { AgentDescriptor, AgentLoopStatus, ServerAutonomy } from '@agent-up/chat/models/AgentSession';
import { presentAgentRow } from '@agent-up/chat/providers/AgentAvailabilityProvider';
import { canActivateLoop, canCancelLoop, canEnableAutonomy, preconditionLabel } from '@agent-up/chat/providers/LoopSurfaceProvider';
import type { SceneSlotItem } from './CommandCenterSceneProvider';

/** What Settings · Chat knows about the workspace it is showing. */
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
 * The unattended loop and the agent a workspace runs, one field per property, the way Desktop's
 * Settings · Chat lists them. A field the client cannot change keeps its control, locked; the
 * remedy the Server sends is the detail that says why.
 */
export function chatSettingsFields(input: ChatSettingsInput): SceneSlotItem[] {
  const agents = input.agents.map(agent => agentField(agent, input.boundAgent));
  const preconditions = input.autonomy?.preconditions ?? [];
  const rows = preconditions.map((item): Field => {
    const detail = item.satisfied ? 'Ready' : item.remedy ?? 'Not ready';
    const title = preconditionLabel(item.id);
    if (item.id === 'autonomyEnabled') {
      return { id: item.id, title, detail, actionLabel: 'Enable', action: 'enable-autonomy', enabled: !item.satisfied && canEnableAutonomy(input.autonomy), children: [loopField(input)] };
    }
    if (item.id === 'agentAvailable') return { id: item.id, title, detail, actionLabel: 'Choose', action: null, enabled: false, children: agents };
    return { id: item.id, title, detail, actionLabel: lockedLabel(item.id), action: null, enabled: false };
  });
  if (agents.length > 0 && !preconditions.some(item => item.id === 'agentAvailable')) {
    rows.push({ id: 'agents', title: 'Agent', detail: 'Choose an agent', actionLabel: 'Choose', action: null, enabled: false, children: agents });
  }
  if (!preconditions.some(item => item.id === 'autonomyEnabled')) rows.push(loopField(input));
  return rows.map(slot);
}

/** The team as tabs; the selected one is the workspace whose chat settings are listed. */
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

function loopField(input: ChatSettingsInput): Field {
  return {
    id: 'loop',
    title: 'Unattended loop',
    detail: loopDetail(input.loop),
    actionLabel: 'Activate',
    action: 'activate-loop',
    enabled: canActivateLoop(input.autonomy, input.loop),
    children: [{ id: 'cancel-loop', title: 'Stop the loop', detail: 'Stops the running loop', actionLabel: 'Cancel', action: 'cancel-loop', enabled: canCancelLoop(input.loop) }],
  };
}

function loopDetail(loop: AgentLoopStatus | null | undefined): string {
  if (!loop) return 'Not running';
  if (loop.active) return loop.taskNumber != null ? `Working #${loop.taskNumber}${loop.taskTitle ? ` · ${loop.taskTitle}` : ''}` : `Running · ${loop.phase}`;
  return loop.blockReason ?? loop.stopReason ?? loop.terminal ?? 'Not running';
}

function lockedLabel(id: string): string {
  switch (id) {
    case 'hostConnectionAuthorized': return 'Connect';
    case 'forgeCapabilityEnabled': return 'Enable';
    case 'permissionPolicy': return 'Set';
    case 'boundAgent': return 'Start';
    default: return 'Change';
  }
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
