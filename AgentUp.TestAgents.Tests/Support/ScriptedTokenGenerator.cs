using AgentUp.TestAgents.Features.Inference.Interfaces;

namespace AgentUp.TestAgents.Tests.Support;

/// <summary>Plays back scripted completions so inference tests never load a GGUF.</summary>
internal sealed class ScriptedTokenGenerator(params string[] completions) : ITokenGenerator
{
    private readonly Queue<string> _completions = new(completions);
    public TimeSpan Delay { get; init; }
    public int Calls { get; private set; }

    public async Task<string> CompleteAsync(string prompt, uint seed, CancellationToken cancellationToken)
    {
        Calls++;
        cancellationToken.ThrowIfCancellationRequested();
        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, cancellationToken);
        if (_completions.Count == 0)
            return "just thinking out loud";
        return _completions.Dequeue();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
