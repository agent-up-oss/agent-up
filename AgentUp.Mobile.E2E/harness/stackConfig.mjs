/**
 * How each test agent is presented to the Server.
 *
 * There are four sign-in shapes but only three agent kinds, and the Server binds one CLI per
 * kind, so the loopback-redirect agent and the device-code agent both take the Codex slot and the
 * stack is started twice rather than letting them collide.
 */
export const AGENT_PROFILES = Object.freeze({
  device: Object.freeze({ agent: 'test-agent2', kind: 'Codex', transport: 'device' }),
  redirect: Object.freeze({ agent: 'test-agent1', kind: 'Codex', transport: 'redirect' }),
  paste: Object.freeze({ agent: 'test-agent3', kind: 'Claude', transport: 'paste' }),
  poll: Object.freeze({ agent: 'test-agent4', kind: 'Cursor', transport: 'poll' }),
});

/** The schemas served by one stack, given which agent takes the Codex slot. */
export function profilesFor(codexSchema) {
  if (codexSchema !== 'device' && codexSchema !== 'redirect') {
    throw new Error(`The Codex slot takes 'device' or 'redirect', not '${codexSchema}'.`);
  }

  return [AGENT_PROFILES[codexSchema], AGENT_PROFILES.paste, AGENT_PROFILES.poll];
}

/**
 * Builds the Server's environment.
 *
 * ASP.NET reads configuration from double-underscore environment variables, so the whole stack is
 * described without writing a settings file. Every agent kind gets its ACP command, its login
 * command, and the transport that login actually implements, which is what stops the Server
 * having to guess the shape from terminal output.
 */
export function serverEnvironment({ profiles, binDir, idpUrl, publicOrigin, dataDir, urls, challengeTimeoutSeconds = 30, completionTimeoutSeconds = 120 }) {
  const environment = {
    ASPNETCORE_URLS: urls,
    // The subject under test is agent sign-in, not Server sign-in.
    AGENTUP_AUTH_DISABLED: 'true',
    AGENTUP_DATA_DIR: dataDir,
    AGENTUP_TEST_IDP_URL: idpUrl,
    AGENTUP_TEST_IDP_PUBLIC_ORIGIN: publicOrigin,
  };

  for (const profile of profiles) {
    const shim = `${binDir}/${profile.agent}`;
    environment[`Agents__${profile.kind}__Command`] = shim;
    environment[`Agents__${profile.kind}__Arguments__0`] = 'acp';
    environment[`Agents__${profile.kind}__LoginCommand`] = shim;
    environment[`Agents__${profile.kind}__LoginArguments__0`] = 'login';
    environment[`Agents__${profile.kind}__LoginTransport`] = profile.transport;
    // Short, explicit deadlines: a hung sign-in should fail the test quickly and legibly rather
    // than stall the suite until the runner's own timeout fires.
    environment[`Agents__${profile.kind}__LoginChallengeTimeoutSeconds`] = String(challengeTimeoutSeconds);
    environment[`Agents__${profile.kind}__LoginCompletionTimeoutSeconds`] = String(completionTimeoutSeconds);
    environment[`Agents__${profile.kind}__LoginEnvironment__AGENTUP_TEST_IDP_URL`] = idpUrl;
    environment[`Agents__${profile.kind}__LoginEnvironment__AGENTUP_TEST_AGENT`] = profile.agent;
  }

  return environment;
}

/**
 * The origin a client reaches the host on.
 *
 * An Android emulator is a separate network namespace and reaches its host as 10.0.2.2; an iOS
 * simulator and the installable web client share the host's loopback. Getting this wrong is not a
 * flake, it is a total failure to connect, so it is named here rather than guessed per test.
 */
export function hostOriginFor(platform, port) {
  if (platform === 'android') return `http://10.0.2.2:${port}`;
  if (platform === 'ios' || platform === 'web') return `http://localhost:${port}`;
  throw new Error(`Unknown client platform '${platform}'.`);
}
