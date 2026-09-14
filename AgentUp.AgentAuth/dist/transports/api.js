/** Talks to the Server's sign-in endpoints. */
export function createAgentLoginApi(options) {
    const call = async (path, body) => {
        const doFetch = options.fetch ?? globalThis.fetch;
        const headers = { 'content-type': 'application/json' };
        if (options.token)
            headers.authorization = `Bearer ${options.token}`;
        const response = await doFetch(`${options.baseUrl.replace(/\/$/, '')}/api/workspaces/${encodeURIComponent(options.workspaceId)}/agent/${path}`, { method: 'POST', headers, body: JSON.stringify(body) });
        if (!response.ok) {
            throw new Error(`Agent sign-in request failed with ${response.status}: ${await safeText(response)}`);
        }
    };
    return {
        submitCode: code => call('login/code', { code }),
        submitCallback: url => call('login/callback', { url }),
    };
}
async function safeText(response) {
    try {
        return (await response.text()).slice(0, 500);
    }
    catch {
        return '<no body>';
    }
}
