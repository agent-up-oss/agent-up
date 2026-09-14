import type { AgentLoginPort } from '../machine/types.js';

export type WebPortOptions = {
  /**
   * How long to keep the popup open waiting for the redirect. It is a bound on a user action, not
   * a poll interval; it exists so an abandoned sign-in resolves instead of leaking a handle.
   */
  timeoutMs?: number;
  /** How often to read the popup's location. Same-origin reads throw until it comes back. */
  pollMs?: number;
};

/**
 * The installable web client.
 *
 * The redirect lands on a loopback address on the Server host, so in a browser it can only be
 * caught by opening the sign-in in a popup this page can still see, and watching its location.
 * Once the popup navigates to the loopback address, the browser is on a cross-origin URL we can
 * still read the href of only while it is same-origin, so the popup is closed as soon as its
 * location matches and the URL is handed back.
 */
export function createWebLoginPort(options: WebPortOptions = {}): AgentLoginPort {
  const timeoutMs = options.timeoutMs ?? 5 * 60 * 1000;
  const pollMs = options.pollMs ?? 250;

  return {
    async openUrl(url) {
      window.open(url, '_blank', 'noopener,noreferrer');
    },

    async openInterceptingRedirect(url, redirectUri) {
      const popup = window.open(url, 'agent-up-sign-in', 'width=520,height=680');
      if (!popup) {
        // Popups blocked: fall back to the plain open so the user is not stuck, and let the
        // Server time the sign-in out rather than pretending it succeeded.
        window.open(url, '_blank', 'noopener,noreferrer');
        return null;
      }

      const deadline = Date.now() + timeoutMs;
      try {
        while (Date.now() < deadline) {
          if (popup.closed) return null;
          const seen = readLocation(popup);
          if (seen && seen.startsWith(redirectUri)) {
            popup.close();
            return seen;
          }

          await delay(pollMs);
        }

        return null;
      } finally {
        if (!popup.closed) popup.close();
      }
    },

    async copy(value) {
      await navigator.clipboard.writeText(value);
    },
  };
}

/** Cross-origin reads throw, which simply means the popup is still on the provider's pages. */
function readLocation(popup: Window): string | null {
  try {
    return popup.location.href;
  } catch {
    return null;
  }
}

function delay(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}
