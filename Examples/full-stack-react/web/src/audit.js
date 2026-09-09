const workspaceId = __AGENT_UP_WORKSPACE_ID__;
const application = __AGENT_UP_APPLICATION__;
const endpoint = __AGENT_UP_AUDIT_ENDPOINT__;

export async function recordAudit(action, outcome, details = {}) {
  if (!workspaceId || !endpoint) {
    return;
  }

  const payload = {
    kind: 'frontend',
    source: 'web',
    action,
    outcome,
    workspaceId,
    details: {
      ...Object.fromEntries(
        Object.entries(details)
          .filter(([, value]) => value !== undefined)
          .map(([key, value]) => [key, value === null ? '' : String(value)]),
      ),
      application,
    },
  };

  try {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(payload),
    });
    if (!response.ok) {
      throw new Error(`Agent-Up audit endpoint returned ${response.status}.`);
    }
  } catch (error) {
    console.warn(`[audit] ${error.message}`);
  }
}
