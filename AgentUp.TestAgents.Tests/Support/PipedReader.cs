namespace AgentUp.TestAgents.Tests.Support;

/// <summary>A reader a test can push a line into once the agent has asked for one.</summary>
internal sealed class PipedReader : TextReader
{
    private readonly TaskCompletionSource<string> _line = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Write(string value) => _line.TrySetResult(value);

    public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
        await _line.Task.WaitAsync(cancellationToken);
}
