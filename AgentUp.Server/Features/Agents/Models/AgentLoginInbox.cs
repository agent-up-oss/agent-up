using System.Threading.Channels;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// Carries codes and intercepted redirects from an HTTP request to the sign-in already running
/// in the background. Unbounded and non-blocking, so a client that posts a code before the CLI
/// has written its prompt is held rather than rejected.
/// </summary>
public sealed class AgentLoginInbox
{
    private readonly Channel<AgentLoginSubmission> _submissions =
        Channel.CreateUnbounded<AgentLoginSubmission>(new UnboundedChannelOptions { SingleReader = true });

    public bool TrySubmit(AgentLoginSubmission submission) => _submissions.Writer.TryWrite(submission);

    public IAsyncEnumerable<AgentLoginSubmission> ReadAllAsync(CancellationToken cancellationToken) =>
        _submissions.Reader.ReadAllAsync(cancellationToken);

    public void Complete() => _submissions.Writer.TryComplete();
}
