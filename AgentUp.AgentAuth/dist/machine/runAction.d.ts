import type { AgentLoginAction, AgentLoginApi, AgentLoginPort } from './types.js';
/** What running an action did, so a client can report it without re-deriving the transport. */
export type AgentLoginOutcome = {
    kind: 'nothingToDo';
} | {
    kind: 'opened';
}
/** The user has to hand a code back; the client should show its code field. */
 | {
    kind: 'awaitingCode';
}
/** A redirect was intercepted and handed to the Server. */
 | {
    kind: 'callbackSubmitted';
    url: string;
}
/** The redirect was never seen, usually because the user closed the browser. */
 | {
    kind: 'abandoned';
};
/**
 * Performs the one action a challenge calls for.
 *
 * Deliberately does not loop or poll: the Server owns the sign-in's progress and pushes state
 * changes, so a client that polled here would be racing it.
 */
export declare function runAction(action: AgentLoginAction, port: AgentLoginPort, api: AgentLoginApi): Promise<AgentLoginOutcome>;
/**
 * Sends a code the user carried out of the browser. Trimming matters: a code copied off a page
 * arrives with whitespace often enough that not trimming it is a real failure.
 */
export declare function submitCode(code: string, api: AgentLoginApi): Promise<boolean>;
