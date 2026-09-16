namespace AgentUp.Browser.Streaming.Models;

internal sealed record BrowserNavigationRequest(Guid CommandId, CancellationTokenSource Cancellation);
