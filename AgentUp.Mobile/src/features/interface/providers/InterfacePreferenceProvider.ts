/**
 * Which interface this client draws.
 *
 * The command center is opt-in and reversible from both sides: the classic Settings screen
 * turns it on, the command center's own Settings turns it off. The preference is local to this
 * client and never reaches the Server, so switching changes nothing about the agents, the
 * workspaces or the accounts behind them.
 */

export type InterfaceChoice = 'classic' | 'command-center';

export const INTERFACE_STORAGE_KEY = 'agent-up.interface';

export interface KeyValueStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

/**
 * Reading `window.localStorage` throws outright when site data is blocked, so the access is
 * guarded rather than assumed. A client that cannot store the preference still runs; it just
 * opens on the classic interface every time.
 */
export function browserInterfaceStorage(): KeyValueStorage | null {
  if (typeof window === 'undefined') return null;
  try {
    // React Native defines `window` but not `window.localStorage`, and reading it there throws
    // nothing — it is simply undefined. Returning that would make the caller's `storage !== null`
    // check report a preference as stored when nothing was.
    return window.localStorage ?? null;
  } catch {
    return null;
  }
}

export function readInterfaceChoice(storage: KeyValueStorage | null): InterfaceChoice {
  if (!storage) return 'classic';
  try {
    return storage.getItem(INTERFACE_STORAGE_KEY) === 'command-center' ? 'command-center' : 'classic';
  } catch {
    return 'classic';
  }
}

/** Returns whether the choice was stored, so a caller can say so rather than assume it stuck. */
export function writeInterfaceChoice(storage: KeyValueStorage | null, choice: InterfaceChoice): boolean {
  if (!storage) return false;
  try {
    storage.setItem(INTERFACE_STORAGE_KEY, choice);
    return true;
  } catch {
    return false;
  }
}
