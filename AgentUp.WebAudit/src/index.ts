export type AuditOutcome = 'success' | 'failure' | 'info';

export interface AgentUpAuditOptions {
  endpoint?: string;
  workspaceId?: string;
  application?: string;
  fetch?: typeof fetch;
}

export interface FrontendAuditEvent {
  action: string;
  outcome?: AuditOutcome;
  details?: Record<string, string | number | boolean | null | undefined>;
}

export interface AgentUpAuditClient {
  record(event: FrontendAuditEvent): Promise<void>;
}

const defaultEndpoint = 'http://127.0.0.1:5000/api/audit/record';

export function createAgentUpAudit(options: AgentUpAuditOptions = {}): AgentUpAuditClient {
  const endpoint = options.endpoint ?? readMeta('agent-up-audit-endpoint') ?? defaultEndpoint;
  const workspaceId = options.workspaceId ?? readMeta('agent-up-workspace-id');
  const application = options.application ?? readMeta('agent-up-application');
  const request = options.fetch ?? globalThis.fetch;

  return {
    async record(event) {
      const details = Object.fromEntries(Object.entries(event.details ?? {})
        .filter((entry): entry is [string, string | number | boolean | null] => entry[1] !== undefined)
        .map(([key, value]) => [key, value === null ? '' : String(value)]));
      if (application) details.application = application;

      let response: Response;
      try {
        response = await request(endpoint, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
          body: JSON.stringify({
            kind: 'frontend', source: 'web', action: event.action,
            outcome: event.outcome ?? 'info', workspaceId: workspaceId ?? null, details,
          }),
        });
      } catch (error) {
        throw connectionError(endpoint, error);
      }
      if (!response.ok) throw new Error(`Agent-Up audit endpoint returned ${response.status}.`);
    },
  };
}

function readMeta(name: string): string | undefined {
  if (typeof document === 'undefined') return undefined;
  return document.querySelector<HTMLMetaElement>(`meta[name="${name}"]`)?.content || undefined;
}

function connectionError(endpoint: string, cause: unknown): Error {
  const target = new URL(endpoint);
  if (typeof location !== 'undefined' && location.protocol === 'https:' && target.protocol === 'http:')
    return new Error('The browser blocked the HTTP Agent-Up server from this HTTPS page. Host the app on loopback HTTP or expose Agent-Up through an approved HTTPS endpoint.', { cause });
  return new Error(`Could not reach the Agent-Up audit endpoint at ${target.origin}.`, { cause });
}
