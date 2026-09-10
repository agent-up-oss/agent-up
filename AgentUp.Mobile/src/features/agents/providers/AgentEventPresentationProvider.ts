export type AgentMessageRole = 'agent' | 'thought' | 'tool' | 'system';

export function agentEventRole(update: unknown): AgentMessageRole {
  const kind = stringField(update, 'sessionUpdate') ?? stringField(update, 'type') ?? '';
  if (kind.includes('thought')) return 'thought';
  if (kind.includes('tool')) return 'tool';
  return kind.includes('message') ? 'agent' : 'system';
}

export function agentEventText(value: unknown): string {
  if (typeof value === 'string') return value;
  if (Array.isArray(value)) return value.map(agentEventText).filter(Boolean).join('\n');
  if (!value || typeof value !== 'object') return '';
  const record = value as Record<string, unknown>;
  const content = agentEventText(record.text ?? record.content ?? record.message ?? record.plan ?? record.entries);
  const title = typeof record.title === 'string' ? record.title : '';
  const status = typeof record.status === 'string' ? record.status : '';
  return [title, content, status].filter(Boolean).join(' · ');
}

function stringField(value: unknown, key: string) {
  return value && typeof value === 'object' && typeof (value as Record<string, unknown>)[key] === 'string'
    ? (value as Record<string, string>)[key]
    : null;
}
