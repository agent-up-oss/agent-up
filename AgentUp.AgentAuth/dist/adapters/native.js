export function createNativeLoginPort(platform) {
    return {
        // An in-app WebView sees every navigation its own content makes, so this platform can catch
        // the loopback redirect and hand it back.
        canInterceptRedirect: true,
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
export function isAwaitedRedirect(navigationUrl, redirectUri) {
    const seen = parse(navigationUrl);
    const expected = parse(redirectUri);
    if (!seen || !expected)
        return false;
    if (seen.port !== expected.port)
        return false;
    if (seen.pathname !== expected.pathname)
        return false;
    return isLoopback(seen.hostname) && isLoopback(expected.hostname);
}
function parse(value) {
    try {
        return new URL(value);
    }
    catch {
        return null;
    }
}
function isLoopback(hostname) {
    return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '[::1]' || hostname === '::1';
}
