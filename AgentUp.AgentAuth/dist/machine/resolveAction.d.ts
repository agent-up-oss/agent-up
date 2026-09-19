import type { AgentLoginAction, AgentLoginChallenge, AgentLoginTransport } from './types.js';
/**
 * Turns a challenge into the single thing the client should do about it.
 *
 * Pure and synchronous on purpose: every platform decision lives in a port, so this can be tested
 * without a browser, a device, or a network.
 */
export declare function resolveAction(challenge: AgentLoginChallenge | null | undefined): AgentLoginAction;
/** An unknown or absent transport behaves like the least demanding one. */
export declare function normalizeTransport(transport: string | null | undefined): AgentLoginTransport;
/** True once a challenge has expired, so a client can offer to start again rather than hang. */
export declare function hasExpired(challenge: AgentLoginChallenge | null | undefined, now?: Date): boolean;
