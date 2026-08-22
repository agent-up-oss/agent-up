import { createAgentUpAudit } from '@agent-up/audit';

export async function recordServerConnectionAudit(
  serverUrl: string,
  outcome: 'success' | 'failure',
  message?: string,
  request?: typeof fetch,
  orchestratorEndpoint = process.env.EXPO_PUBLIC_AGENT_UP_AUDIT_ENDPOINT,
): Promise<void> {
  const audit = createAgentUpAudit({
    endpoint: orchestratorEndpoint ?? `${serverUrl}/api/audit/record`,
    workspaceId: process.env.EXPO_PUBLIC_AGENT_UP_WORKSPACE_ID,
    application: process.env.EXPO_PUBLIC_AGENT_UP_APPLICATION ?? 'Mobile',
    fetch: request,
  });

  try {
    await audit.record({
      action: outcome === 'success' ? 'server_connection_succeeded' : 'server_connection_failed',
      outcome,
      details: { serverUrl, message },
    });
  } catch (error) {
    // Audit delivery is best-effort and must not replace the connection result.
    if (!(error instanceof Error)) throw error;
  }
}
