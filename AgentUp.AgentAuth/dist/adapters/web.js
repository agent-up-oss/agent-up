/**
 * The installable web client.
 *
 * This adapter deliberately does not intercept redirects. The agent CLI's callback is on a
 * different origin from this page, so the browser will not let it read that navigation, and there
 * is no way to inject script into a page the CLI serves. Pretending otherwise would mean a
 * sign-in that silently hangs.
 *
 * It does not need to. The redirect targets loopback on the Server's host, so whenever the browser
 * is on that host — which is the only arrangement where a browser can complete this sign-in at all
 * — it reaches the CLI's own listener directly and the Server sees the sign-in finish. Where the
 * browser is elsewhere, the sign-in genuinely cannot complete from a browser, and the Server's own
 * deadline reports that rather than this client claiming success.
 */
export function createWebLoginPort() {
    return {
        canInterceptRedirect: false,
        async openUrl(url) {
            window.open(url, '_blank', 'noopener,noreferrer');
        },
        async openInterceptingRedirect() {
            throw new Error('A browser cannot observe the agent CLI\'s loopback redirect.');
        },
        async copy(value) {
            await navigator.clipboard.writeText(value);
        },
    };
}
