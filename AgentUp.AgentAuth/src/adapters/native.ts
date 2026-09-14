import type { AgentLoginPort } from '../machine/types.js';

/**
 * The platform pieces the native adapter needs, passed in rather than imported, so this module
 * stays testable and carries no React Native import of its own.
 */
export type NativePlatform = {
  /** expo-linking's openURL, or anything with the same shape. */
  openUrl(url: string): Promise<unknown>;
  /** Clipboard write. */
  copy(value: string): Promise<unknown>;
  /**
   * Opens an in-app WebView and resolves with the first navigation whose URL starts with the
   * given prefix, or null when the user dismisses it.
   *
   * This cannot be the system browser. A redirect to http://localhost on the Server host is only
   * observable from inside a WebView whose navigations this app can see, which on React Native
   * means react-native-webview's onShouldStartLoadWithRequest. Linking.openURL hands the URL to
   * Safari or Chrome and never gets anything back.
   */
  openInterceptingWebView(url: string, redirectPrefix: string): Promise<string | null>;
};

export function createNativeLoginPort(platform: NativePlatform): AgentLoginPort {
  return {
    async openUrl(url) {
      await platform.openUrl(url);
    },

    async openInterceptingRedirect(url, redirectUri) {
      return platform.openInterceptingWebView(url, redirectUri);
    },

    async copy(value) {
      await platform.copy(value);
    },
  };
}

/**
 * Whether a navigation is the redirect being waited for.
 *
 * The agent CLI binds one of localhost or 127.0.0.1 and prints that spelling, but a provider or a
 * WebView may hand back the other, so both are accepted for the same port and path.
 */
export function isAwaitedRedirect(navigationUrl: string, redirectUri: string): boolean {
  const seen = parse(navigationUrl);
  const expected = parse(redirectUri);
  if (!seen || !expected) return false;
  if (seen.port !== expected.port) return false;
  if (seen.pathname !== expected.pathname) return false;
  return isLoopback(seen.hostname) && isLoopback(expected.hostname);
}

function parse(value: string): URL | null {
  try {
    return new URL(value);
  } catch {
    return null;
  }
}

function isLoopback(hostname: string): boolean {
  return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '[::1]' || hostname === '::1';
}
