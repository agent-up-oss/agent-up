import { isAwaitedRedirect } from '@agent-up/agent-auth/native';

/**
 * Decides whether a WebView navigation is the redirect being waited for.
 *
 * Returning false lets the WebView carry on loading; returning true means this navigation is the
 * callback and must be stopped here and handed to the Server instead of being followed, because
 * following it inside the phone's WebView would only ever reach the phone's own loopback.
 *
 * Kept free of any React Native import so the rule can be tested under plain Node.
 */
export function shouldInterceptNavigation(navigationUrl: string, redirectUri: string): boolean {
  return isAwaitedRedirect(navigationUrl, redirectUri);
}
