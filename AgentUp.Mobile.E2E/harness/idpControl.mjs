/**
 * The identity provider's test control plane.
 *
 * Every approval here is an explicit act at a moment the test chooses. Nothing in this suite
 * waits out a polling interval or drives the provider's DOM to get a sign-in approved, because
 * both of those are how browser-driven auth tests become flaky.
 */
export function createIdpControl(idpUrl, request = fetch) {
  const origin = idpUrl.replace(/\/$/, '');

  const post = async (path, form) => {
    const response = await request(`${origin}${path}`, {
      method: 'POST',
      headers: { 'content-type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(form).toString(),
    });
    if (!response.ok) {
      throw new Error(`${path} failed with ${response.status}: ${await response.text()}`);
    }
    return response.json();
  };

  return {
    /**
     * Approves this client's authorization requests up front, so the redirect happens the instant
     * the browser opens the link. That is what lets a redirect sign-in be driven end to end
     * without clicking anything in a WebView.
     */
    preApprove: clientId => post('/test/pre-approve', { client_id: clientId }),

    /** Approves a device authorization by the code the user was shown. */
    approveUserCode: userCode => post('/test/approve', { user_code: userCode }),

    /** Approves a poll-until-approved sign-in by the id in its deep link. */
    approveLogin: loginId => post('/test/approve', { login_id: loginId }),

    /** The code most recently issued to a client, for a test that has to paste it back. */
    latestCode: async clientId => {
      const response = await request(`${origin}/test/latest-code?client_id=${encodeURIComponent(clientId)}`);
      if (!response.ok) throw new Error(`No code has been issued to ${clientId} yet.`);
      return (await response.json()).code;
    },

    reset: () => post('/test/reset', {}),

    health: async () => {
      const response = await request(`${origin}/health`);
      return response.ok;
    },
  };
}

/** Pulls the login id out of a poll-shaped deep link. */
export function loginIdFrom(loginUrl) {
  const match = /\/login\/([A-Za-z0-9_-]+)/.exec(loginUrl ?? '');
  if (!match) throw new Error(`'${loginUrl}' is not a sign-in deep link.`);
  return match[1];
}
