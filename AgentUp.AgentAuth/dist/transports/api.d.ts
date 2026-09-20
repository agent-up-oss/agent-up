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
export declare function createAgentLoginApi(options: AgentLoginApiOptions): AgentLoginApi;
