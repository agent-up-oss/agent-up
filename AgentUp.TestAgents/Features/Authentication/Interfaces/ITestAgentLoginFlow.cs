namespace AgentUp.TestAgents.Features.Authentication.Interfaces;

/// <summary>
/// One agent CLI's sign-in, run as its own process exactly as the Server runs the real ones:
/// it writes progress to the terminal, may read stdin, and exits non-zero when it fails.
/// </summary>
public interface ITestAgentLoginFlow
{
    /// <summary>The client id this agent presents to the identity provider.</summary>
    string ClientId { get; }

    /// <summary>
    /// Runs the sign-in to completion and returns the token, or null when it failed. Writes to
    /// <paramref name="output"/> the way the real CLI writes to its terminal.
    /// </summary>
    Task<string?> RunAsync(TextWriter output, TextReader input, CancellationToken cancellationToken);
}
