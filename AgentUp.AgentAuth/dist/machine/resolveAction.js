/**
 * Turns a challenge into the single thing the client should do about it.
 *
 * Pure and synchronous on purpose: every platform decision lives in a port, so this can be tested
 * without a browser, a device, or a network.
 */
export function resolveAction(challenge) {
    const waiting = { kind: 'wait', message: 'Waiting for the agent to print a sign-in link.' };
    if (!challenge || !challenge.url)
        return waiting;
    const url = challenge.url;
    const message = challenge.instructions ?? 'Open this link and sign in with your subscription.';
    const transport = normalizeTransport(challenge.transport);
    if (transport === 'redirect') {
        // Without the loopback address there is nothing to watch for, so the best the client can do
        // is open the link and hope the Server host sees the redirect itself.
        if (!challenge.redirectUri)
            return { kind: 'open', url, message };
        return { kind: 'interceptRedirect', url, redirectUri: challenge.redirectUri, message };
    }
    if (transport === 'code') {
        // A code the Server already knows is one the user types into the page, so nothing comes back.
        if (challenge.code)
            return { kind: 'openWithCode', url, code: challenge.code, message };
        // Otherwise the code travels the other way, and `canSubmitCode` says whether the CLI is
        // actually waiting for it yet.
        return { kind: 'collectCode', url, message, ready: challenge.canSubmitCode === true };
    }
    if (challenge.code)
        return { kind: 'openWithCode', url, code: challenge.code, message };
    return { kind: 'open', url, message };
}
/** An unknown or absent transport behaves like the least demanding one. */
export function normalizeTransport(transport) {
    const value = (transport ?? '').toLowerCase();
    if (value === 'poll' || value === 'code' || value === 'redirect')
        return value;
    return 'unknown';
}
/** True once a challenge has expired, so a client can offer to start again rather than hang. */
export function hasExpired(challenge, now = new Date()) {
    if (!challenge?.expiresAt)
        return false;
    const expiresAt = Date.parse(challenge.expiresAt);
    return Number.isFinite(expiresAt) && expiresAt <= now.getTime();
}
