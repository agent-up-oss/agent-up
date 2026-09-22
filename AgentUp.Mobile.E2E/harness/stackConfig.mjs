/**
 * How each test agent is presented to the Server.
 *
 * `agentId` is the capability module id the Server lists agents by, which is what the client
 * renders its picker from. There are four sign-in shapes but only three agent modules, and the
 * Server binds one CLI per module, so the loopback-redirect agent and the device-code agent both
 * take the codex slot and the stack is started twice rather than letting them collide.
 */
export const AGENT_PROFILES = Object.freeze({
  device: Object.freeze({ agent: 'test-agent2', agentId: 'codex', transport: 'device' }),
  redirect: Object.freeze({ agent: 'test-agent1', agentId: 'codex', transport: 'redirect' }),
  paste: Object.freeze({ agent: 'test-agent3', agentId: 'claude', transport: 'paste' }),
  poll: Object.freeze({ agent: 'test-agent4', agentId: 'cursor', transport: 'poll' }),
});

/** The schemas served by one stack, given which agent takes the codex slot. */
export function profilesFor(codexSchema) {
  if (codexSchema !== 'device' && codexSchema !== 'redirect') {
    throw new Error(`The codex slot takes 'device' or 'redirect', not '${codexSchema}'.`);
  }

  return [AGENT_PROFILES[codexSchema], AGENT_PROFILES.paste, AGENT_PROFILES.poll];
}

/**
 * Builds the Server's environment.
 *
 * ASP.NET reads configuration from double-underscore environment variables, so the whole stack is
 * described without writing a settings file. Every agent module gets its ACP command, its login
 * command, and the transport that login actually implements, which is what stops the Server
 * having to guess the shape from terminal output.
 *
 * The completion window is deliberately longer than any wait in the suites. It is the Server's
 * patience with a sign-in that is under way, not an assertion, and nothing here tests it - so the
 * moment it is the shorter of the two deadlines, it is the one that fires, and a scenario reports
 * "the sign-in was not completed within 2 minutes" instead of what the test was actually waiting
 * for. That is how the device-code scenario failed on a simulator whose browser had never been
 * launched before: opening the sign-in page took long enough, cold, to spend the window before the
 * code was ever approved. A person on a warm phone does not wait that long, and the suite is not
 * there to find out what happens when they do.
 */
export function serverEnvironment({ profiles, binDir, idpUrl, publicOrigin, dataDir, urls, challengeTimeoutSeconds = 30, completionTimeoutSeconds = 300 }) {
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
    environment[`Agents__${profile.agentId}__Command`] = shim;
    environment[`Agents__${profile.agentId}__Arguments__0`] = 'acp';
    environment[`Agents__${profile.agentId}__LoginCommand`] = shim;
    environment[`Agents__${profile.agentId}__LoginArguments__0`] = 'login';
    environment[`Agents__${profile.agentId}__LoginTransport`] = profile.transport;
    // Short, explicit deadlines: a hung sign-in should fail the test quickly and legibly rather
    // than stall the suite until the runner's own timeout fires.
    environment[`Agents__${profile.agentId}__LoginChallengeTimeoutSeconds`] = String(challengeTimeoutSeconds);
    environment[`Agents__${profile.agentId}__LoginCompletionTimeoutSeconds`] = String(completionTimeoutSeconds);
    environment[`Agents__${profile.agentId}__LoginEnvironment__AGENTUP_TEST_IDP_URL`] = idpUrl;
    // Two origins, because the agent and the person are not in the same place. The agent runs on
    // this host and reaches the provider at idpUrl; the person is on a simulator or an emulator,
    // for which 10.0.2.2 is this host and localhost is the device. A link printed on the agent's
    // own origin is one the device cannot open, which is exactly how the loopback-redirect
    // scenario failed on Android while the others happened not to need the link to work.
    environment[`Agents__${profile.agentId}__LoginEnvironment__AGENTUP_TEST_IDP_PUBLIC_ORIGIN`] = publicOrigin;
    environment[`Agents__${profile.agentId}__LoginEnvironment__AGENTUP_TEST_AGENT`] = profile.agent;
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
