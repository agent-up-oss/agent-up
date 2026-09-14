import type { AgentLoginApi } from '../machine/types.js';

export type AgentLoginApiOptions = {
  /** Server origin, for example https://localhost:7001 */
  baseUrl: string;
  workspaceId: string;
  /** Bearer token for the Server, when it requires one. */
  token?: string | undefined;
  /** Injected so tests drive a real server without a global fetch. */
  fetch?: typeof globalThis.fetch | undefined;
};

/** Talks to the Server's sign-in endpoints. */
export function createAgentLoginApi(options: AgentLoginApiOptions): AgentLoginApi {
  const call = async (path: string, body: unknown): Promise<void> => {
    const doFetch = options.fetch ?? globalThis.fetch;
    const headers: Record<string, string> = { 'content-type': 'application/json' };
    if (options.token) headers.authorization = `Bearer ${options.token}`;

    const response = await doFetch(
      `${options.baseUrl.replace(/\/$/, '')}/api/workspaces/${encodeURIComponent(options.workspaceId)}/agent/${path}`,
      { method: 'POST', headers, body: JSON.stringify(body) },
    );

    if (!response.ok) {
      throw new Error(`Agent sign-in request failed with ${response.status}: ${await safeText(response)}`);
    }
  };

  return {
    submitCode: code => call('login/code', { code }),
    submitCallback: url => call('login/callback', { url }),
  };
}

async function safeText(response: Response): Promise<string> {
  try {
    return (await response.text()).slice(0, 500);
  } catch {
    return '<no body>';
  }
}
