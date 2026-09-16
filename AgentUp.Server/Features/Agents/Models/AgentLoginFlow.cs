using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// What a given agent CLI's sign-in actually does, declared up front instead of guessed from
/// its terminal output. The Server spawned the CLI, so it already knows which flow that CLI
/// implements; discovering it from printed text was the thing that kept breaking.
/// </summary>
/// <param name="Transport">What the client has to do to complete the sign-in.</param>
/// <param name="NeedsCodeInput">
/// True when the CLI blocks on stdin for a code the user copies out of the browser, the way
/// <c>claude setup-token</c> does. The prompt it writes has no trailing newline, so the reader
/// has to surface partial output for these.
/// </param>
/// <param name="ChallengeTimeout">How long the CLI may take to print its sign-in link.</param>
/// <param name="CompletionTimeout">How long the whole sign-in may take once the link is out.</param>
public sealed record AgentLoginFlow(
    AgentLoginTransport Transport,
    bool NeedsCodeInput,
    TimeSpan ChallengeTimeout,
    TimeSpan CompletionTimeout)
{
    public static AgentLoginFlow Poll() =>
        new(AgentLoginTransport.Poll, false, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(10));

    public static AgentLoginFlow DeviceCode() =>
        new(AgentLoginTransport.Code, false, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(10));

    public static AgentLoginFlow PastedCode() =>
        new(AgentLoginTransport.Code, true, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(10));

    public static AgentLoginFlow Redirect() =>
        new(AgentLoginTransport.Redirect, false, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(10));
}
