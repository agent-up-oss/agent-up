import { loginIdFrom } from './idpControl.mjs';
import { waitFor } from './wait.mjs';

/**
 * What each sign-in shape needs from the world outside the client, expressed once so the Detox
 * suites and the installable-web suite prove the same thing rather than drifting apart.
 *
 * Each entry says: which client id the agent presents, what the Server has to be doing before the
 * scenario can proceed, and how the sign-in gets approved. None of them sleep.
 */
export const SIGN_IN_FLOWS = Object.freeze({
  device: {
    clientId: 'test-agent2',
    kind: 'Codex',
    transport: 'code',
    /** The user reads a code off the app and types it into the provider page. */
    async approve({ control, session }) {
      const userCode = session.loginChallenge?.code;
      if (!userCode) throw new Error('The Server never surfaced a user code for the device sign-in.');
      await control.approveUserCode(userCode);
    },
  },

  poll: {
    clientId: 'test-agent4',
    kind: 'Cursor',
    transport: 'poll',
    /** Nothing is carried by hand; approving the login id is all it takes. */
    async approve({ control, session }) {
      await control.approveLogin(loginIdFrom(session.loginChallenge?.url));
    },
  },

  paste: {
    clientId: 'test-agent3',
    kind: 'Claude',
    transport: 'code',
    /**
     * The provider page has to be visited before it will have issued a code, and the code then
     * travels back through the client. Pre-approving means visiting the link is enough.
     */
    async approve({ control, session }) {
      await control.preApprove('test-agent3');
      // The link is the one the device was shown, so it is on the device's origin; this is not the
      // device. Same page, from here.
      const page = await fetch(control.reachable(session.loginChallenge.url));
      if (!page.ok) throw new Error(`The sign-in page answered ${page.status}.`);
      return control.latestCode('test-agent3');
    },
  },

  redirect: {
    clientId: 'test-agent1',
    kind: 'Codex',
    transport: 'redirect',
    /**
     * Pre-approved before the client opens anything, so the authorization request redirects
     * immediately and the client's interception path runs for real.
     */
    async beforeStart({ control }) {
      await control.preApprove('test-agent1');
    },
  },
});

/**
 * True when a challenge carries the transport a scenario expects.
 *
 * The Server serialises the transport as its enum member name, so the wire carries 'Code' where
 * the client's own contract says 'code'. The client already folds case before branching; this
 * folds it the same way rather than pinning the suite to one spelling of the same value.
 */
export function hasTransport(challenge, transport) {
  return (challenge?.transport ?? '').toLowerCase() === transport;
}

/** Reads an agent session off the Server. */
export async function readSession(serverUrl, workspaceId) {
  const response = await fetch(`${serverUrl}/api/workspaces/${encodeURIComponent(workspaceId)}/agent`);
  if (!response.ok) throw new Error(`Reading the agent session failed with ${response.status}.`);
  return response.json();
}

/** Waits for the Server to report a given agent state, with the last state named on failure. */
export async function waitForAgentState(serverUrl, workspaceId, expected, options = {}) {
  let seen = 'none';
  let last = null;
  try {
    return await waitFor(
      `the agent to reach '${expected}'`,
      async () => {
        const session = await readSession(serverUrl, workspaceId);
        seen = session.state;
        last = session;
        return session.state === expected ? session : false;
      },
      options,
    );
  } catch (cause) {
    throw new Error(`${cause.message} The agent was last seen in '${seen}'.${describe(last)}`);
  }
}

/** Waits for a sign-in challenge that carries everything the client needs to act on it. */
export async function waitForChallenge(serverUrl, workspaceId, predicate, options = {}) {
  let last = null;
  try {
    return await waitFor(
      'the agent to publish a usable sign-in challenge',
      async () => {
        const session = await readSession(serverUrl, workspaceId);
        last = session;
        return session.loginChallenge && predicate(session.loginChallenge) ? session : false;
      },
      options,
    );
  } catch (cause) {
    throw new Error(`${cause.message}${describe(last)}`);
  }
}

/**
 * What the Server last reported, for a wait that gave up.
 *
 * A sign-in that fails leaves its reason on the session, so a timeout that does not carry it
 * turns every failure into a fresh investigation.
 */
function describe(session) {
  if (!session) return ' The Server was never read successfully.';
  const parts = [`state '${session.state}'`];
  if (session.error) parts.push(`error '${session.error}'`);
  parts.push(`challenge ${JSON.stringify(session.loginChallenge ?? null)}`);
  return ` The Server last reported ${parts.join(', ')}.`;
}
