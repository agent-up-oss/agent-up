using System.Text;

namespace AgentUp.TestAgents.Tests.Support;

/// <summary>
/// Collects what an agent writes to its terminal while a test reads it.
/// <para>
/// The agent writes from its own task while the test polls, so every access is synchronized. An
/// unsynchronized StringWriter here would be a genuine flake source.
/// </para>
/// </summary>
internal sealed class AgentOutput : TextWriter
{
    private readonly StringBuilder _written = new();
    private readonly Lock _gate = new();

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        lock (_gate) _written.Append(value);
    }

    public override void Write(string? value)
    {
        lock (_gate) _written.Append(value);
    }

    public override string ToString()
    {
        lock (_gate) return _written.ToString();
    }

    /// <summary>
    /// Waits for the agent to print a line containing <paramref name="needle"/>, returning it from
    /// its first URL onwards when it has one.
    /// </summary>
    /// <remarks>
    /// Polls the output rather than sleeping a fixed amount, so a test is bounded by a deadline
    /// instead of by a guess about how fast the agent got there.
    /// </remarks>
    public Task<string> WaitForLineContainingAsync(string needle, CancellationToken cancellationToken) =>
        WaitForLineAsync(
            line => line.Contains(needle, StringComparison.Ordinal),
            $"a line containing '{needle}'",
            cancellationToken);

    /// <summary>Waits for the first printed line the caller recognises.</summary>
    public async Task<string> WaitForLineAsync(
        Func<string, bool> wanted,
        string description,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var match = ToString()
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .FirstOrDefault(wanted);
            if (match is not null)
                return match.Contains("http", StringComparison.Ordinal)
                    ? match[match.IndexOf("http", StringComparison.Ordinal)..]
                    : match;
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        throw new TimeoutException($"The agent never printed {description}. Output so far:\n{this}");
    }
}
