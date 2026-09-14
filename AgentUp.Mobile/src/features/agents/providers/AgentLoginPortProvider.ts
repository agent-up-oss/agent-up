import * as Linking from 'expo-linking';
import { createNativeLoginPort } from '@agent-up/agent-auth/native';
import type { AgentLoginPort } from '@agent-up/agent-auth';

/**
 * A redirect sign-in the client has to watch, handed to the screen so it can mount the WebView
 * and resolve this request when the navigation arrives.
 */
export type PendingRedirect = {
  url: string;
  redirectUri: string;
  settle: (callbackUrl: string | null) => void;
};

export type AgentLoginPortOptions = {
  /**
   * Mounts the in-app WebView. This has to be a WebView rather than the system browser: the
   * redirect lands on a loopback address on the Server host, and only a WebView whose navigations
   * this app can see will ever hand that back. Linking.openURL gives the URL to Safari or Chrome
   * and nothing returns.
   */
  requestRedirectWebView: (pending: PendingRedirect) => void;
  copy: (value: string) => Promise<unknown>;
};

export function createAgentLoginPort(options: AgentLoginPortOptions): AgentLoginPort {
  return createNativeLoginPort({
    openUrl: url => Linking.openURL(url),
    copy: options.copy,
    openInterceptingWebView: (url, redirectPrefix) =>
      new Promise<string | null>(resolve => {
        let settled = false;
        options.requestRedirectWebView({
          url,
          redirectUri: redirectPrefix,
          settle: callbackUrl => {
            if (settled) return;
            settled = true;
            resolve(callbackUrl);
          },
        });
      }),
  });
}
