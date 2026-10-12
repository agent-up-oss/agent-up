namespace AgentUp.TestAgents.Features.Inference.Models;

/// <summary>
/// A replaceable linked cancellation source for one ACP turn, so <c>session/cancel</c>
/// stops the current generation without tearing down the process.
/// </summary>
public sealed class LinkedTurnCancellation(CancellationToken parent) : IDisposable
{
    private CancellationTokenSource _current = CancellationTokenSource.CreateLinkedTokenSource(parent);

    public CancellationToken Token => _current.Token;

    public void Cancel() => _current.Cancel();

    public void BeginNext()
    {
        var previous = _current;
        _current = CancellationTokenSource.CreateLinkedTokenSource(parent);
        previous.Dispose();
    }

    public void Dispose() => _current.Dispose();
}
