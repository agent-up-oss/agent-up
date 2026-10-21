using System.Text;

namespace AgentUp.Server.Features.Processes.Providers;

public sealed class ProcessOutputFramer
{
    private readonly StringBuilder _carry = new();

    public IReadOnlyList<string> Push(string chunk)
        => Push(chunk.AsSpan());

    public IReadOnlyList<string> Push(ReadOnlySpan<char> chunk)
    {
        if (chunk.IsEmpty)
            return [];

        var lines = new List<string>();
        var start = 0;
        for (var index = 0; index < chunk.Length; index++)
        {
            if (chunk[index] != '\n')
                continue;

            _carry.Append(chunk[start..index]);
            lines.Add(TakeCompletedLine());
            start = index + 1;
        }

        if (start < chunk.Length)
            _carry.Append(chunk[start..]);

        return lines;
    }

    public string? Flush()
    {
        if (_carry.Length == 0)
            return null;

        var line = _carry.ToString();
        _carry.Clear();
        return line;
    }

    private string TakeCompletedLine()
    {
        if (_carry.Length > 0 && _carry[^1] == '\r')
            _carry.Length--;

        var line = _carry.ToString();
        _carry.Clear();
        return line;
    }
}
