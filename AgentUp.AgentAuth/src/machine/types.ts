/**
 * How the user completes a sign-in, as the Server reports it.
 *
 * The client branches on this and never on which agent it is talking to. That is the whole point
 * of this module: Claude, Codex, Cursor and the test agents all arrive as one of these three, so
 * the test agents exercise the same code path the real ones do rather than a parallel one.
 */
export type AgentLoginTransport = 'unknown' | 'poll' | 'code' | 'redirect';

/** The sign-in challenge on an agent session, mirroring AgentLoginChallengeDto. */
export type AgentLoginChallenge = {
  url: string | null;
  code: string | null;
  instructions: string | null;
  transport?: AgentLoginTransport;
  /** True once the agent CLI is actually waiting for a code to be sent back. */
  canSubmitCode?: boolean;
  expiresAt?: string | null;
  /**
   * The loopback address the agent CLI is listening on. Present only for the redirect transport,
   * and the value a client watches for so it knows which navigation to intercept.
   */
  redirectUri?: string | null;
};

/** What the client should do right now. */
export type AgentLoginAction =
  /** Nothing to do yet; the CLI has not printed a link. */
  | { kind: 'wait'; message: string }
  /** Show the link and let the user open it. Nothing comes back. */
  | { kind: 'open'; url: string; message: string }
  /** Show the link and a code the user types into the page. Nothing comes back. */
  | { kind: 'openWithCode'; url: string; code: string; message: string }
  /** Open the link, then collect a code the user copies out of the page. */
  | { kind: 'collectCode'; url: string; message: string; ready: boolean }
  /**
   * Open the link inside something that can watch navigations, because the redirect lands on a
   * loopback address on the Server host and has to be carried back by hand.
   */
  | { kind: 'interceptRedirect'; url: string; redirectUri: string; message: string };

/** What the client can do about a sign-in, independent of platform. */
export type AgentLoginPort = {
  /** Open a URL in whatever browser the platform offers. */
  openUrl(url: string): Promise<void>;
  /**
   * Open a URL somewhere navigations can be observed, resolving with the first navigation whose
   * URL starts with `redirectUri`. Resolves null when the user gives up.
   */
  openInterceptingRedirect(url: string, redirectUri: string): Promise<string | null>;
  /** Put a value on the clipboard, for a link or code the user has to carry by hand. */
  copy(value: string): Promise<void>;
};

/** The Server calls a client makes to finish a sign-in. */
export type AgentLoginApi = {
  submitCode(code: string): Promise<void>;
  submitCallback(url: string): Promise<void>;
};
