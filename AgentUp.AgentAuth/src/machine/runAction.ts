import type { AgentLoginAction, AgentLoginApi, AgentLoginPort } from './types.js';

/** What running an action did, so a client can report it without re-deriving the transport. */
export type AgentLoginOutcome =
  | { kind: 'nothingToDo' }
  | { kind: 'opened' }
  /** The user has to hand a code back; the client should show its code field. */
  | { kind: 'awaitingCode' }
  /** A redirect was intercepted and handed to the Server. */
  | { kind: 'callbackSubmitted'; url: string }
  /** The redirect was never seen, usually because the user closed the browser. */
  | { kind: 'abandoned' };

/**
 * Performs the one action a challenge calls for.
 *
 * Deliberately does not loop or poll: the Server owns the sign-in's progress and pushes state
 * changes, so a client that polled here would be racing it.
 */
export async function runAction(
  action: AgentLoginAction,
  port: AgentLoginPort,
  api: AgentLoginApi,
): Promise<AgentLoginOutcome> {
  switch (action.kind) {
    case 'wait':
      return { kind: 'nothingToDo' };

    case 'open':
    case 'openWithCode':
      await port.openUrl(action.url);
      return { kind: 'opened' };

    case 'collectCode':
      // Opening is all the client can do; the code arrives later through submitCode.
      await port.openUrl(action.url);
      return { kind: 'awaitingCode' };

    case 'interceptRedirect': {
      const callback = await port.openInterceptingRedirect(action.url, action.redirectUri);
      if (!callback) return { kind: 'abandoned' };
      await api.submitCallback(callback);
      return { kind: 'callbackSubmitted', url: callback };
    }
  }
}

/**
 * Sends a code the user carried out of the browser. Trimming matters: a code copied off a page
 * arrives with whitespace often enough that not trimming it is a real failure.
 */
export async function submitCode(code: string, api: AgentLoginApi): Promise<boolean> {
  const trimmed = code.trim();
  if (trimmed.length === 0) return false;
  await api.submitCode(trimmed);
  return true;
}
