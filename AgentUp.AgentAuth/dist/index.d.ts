/**
 * Client-side agent sign-in for Agent-Up.
 *
 * One transport state machine serves the real agent CLIs (Claude, Codex, Cursor) and the test
 * agents alike, because it branches on the transport the Server reports and never on which agent
 * is signing in.
 */
export type { AgentLoginAction, AgentLoginApi, AgentLoginChallenge, AgentLoginPort, AgentLoginTransport, } from './machine/types.js';
export { hasExpired, normalizeTransport, resolveAction } from './machine/resolveAction.js';
export { runAction, submitCode } from './machine/runAction.js';
export type { AgentLoginOutcome } from './machine/runAction.js';
export { createAgentLoginApi } from './transports/api.js';
export type { AgentLoginApiOptions } from './transports/api.js';
