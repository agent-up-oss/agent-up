/**
 * What the chat composer offers right now.
 *
 * A running turn does not block the composer: the Server queues what is sent then as steering
 * for the next turn, so the field stays editable and Send becomes Steer. What does block it is a
 * permission request waiting for an answer, a sign-in in progress, or a send still in flight.
 * The field and the button follow the same rule; a Steer button over a read-only field offers an
 * action nobody can take.
 */
export interface ComposerInput {
  /** The session's state, such as running, ready or authentication_required. */
  state: string | null | undefined;
  /** A permission request is waiting for an answer. */
  permission: boolean;
  /** A send is in flight. */
  busy: boolean;
}

export interface ComposerState {
  /** The message will be queued as steering for the next turn. */
  steering: boolean;
  /** Nothing can be sent until what is waiting resolves. */
  blocked: boolean;
  /** The field accepts typing. */
  editable: boolean;
  /** Steer while a turn runs, Send otherwise. */
  action: 'Send' | 'Steer';
}

export function composerState({ state, permission, busy }: ComposerInput): ComposerState {
  const running = state === 'running';
  const signingIn = state === 'authentication_required' || state === 'authenticating';
  const steering = running && !permission;
  const waiting = busy || running || permission || signingIn;
  const blocked = waiting && !steering;
  return { steering, blocked, editable: !blocked, action: steering ? 'Steer' : 'Send' };
}
