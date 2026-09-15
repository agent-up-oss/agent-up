namespace AgentUp.TestAgents.Features.Host.Models;

/// <summary>Which sign-in an agent implements. One test agent per real-world shape.</summary>
public enum TestAgentSchema
{
    /// <summary>
    /// Authorization code with PKCE, answered on a loopback listener the agent opens itself.
    /// This is the shape <c>codex login</c> uses by default, and the one that breaks whenever
    /// the browser is not on the same host as the agent.
    /// </summary>
    LoopbackRedirect,

    /// <summary>
    /// Device authorization: print a verification URL and a user code, then poll. Nothing
    /// listens on a socket, so this shape works unchanged on a phone. Matches
    /// <c>codex login --device-auth</c>.
    /// </summary>
    DeviceCode,

    /// <summary>
    /// Print a link, then block on stdin for a code the user copies out of the browser. Matches
    /// <c>claude setup-token</c>, including the prompt written without a trailing newline.
    /// </summary>
    PastedCode,

    /// <summary>
    /// Print an opaque deep link tied to a login id and poll silently until it is approved.
    /// No user code is ever shown. Matches <c>cursor-agent login</c>.
    /// </summary>
    SilentPoll
}

/// <summary>What the process was asked to do.</summary>
public enum TestAgentVerb
{
    /// <summary>Serve ACP over stdio.</summary>
    Acp,

    /// <summary>Run the sign-in for this agent's schema.</summary>
    Login,

    /// <summary>Run the identity provider the test agents sign in against.</summary>
    IdentityProvider
}

/// <param name="Schema">The sign-in shape, ignored for <see cref="TestAgentVerb.IdentityProvider"/>.</param>
/// <param name="Verb">What to run.</param>
/// <param name="IdentityProviderUrl">Origin of the identity provider to sign in against.</param>
/// <param name="Port">Port for the identity provider to listen on. 0 picks a free one.</param>
/// <param name="PublicOrigin">
/// The origin a client can actually reach the identity provider on. An Android emulator reaches
/// the host as 10.0.2.2, not localhost, so links handed to a client have to be written with the
/// origin that client can open rather than the one the provider bound.
/// </param>
public sealed record TestAgentCommand(
    TestAgentSchema Schema,
    TestAgentVerb Verb,
    string? IdentityProviderUrl,
    int Port,
    string? PublicOrigin);
