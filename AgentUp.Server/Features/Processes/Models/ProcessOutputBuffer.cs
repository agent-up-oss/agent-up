namespace AgentUp.Server.Features.Processes.Models;

public sealed class ProcessOutputBuffer
{
    public const int DefaultCapacity = 50_000;
    public const string TruncationPrefix = "[truncated]";

    private readonly int _capacity;
    private readonly Queue<ProcessOutputLine> _lines = new();
    private int _dropped;

    public ProcessOutputBuffer(int capacity = DefaultCapacity)
    {
        if (capacity < 2)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Console capacity must leave room for a truncation marker.");

        _capacity = capacity;
    }

    public int DroppedCount => _dropped;

    public void Append(ProcessOutputLine line)
    {
        _lines.Enqueue(line);
        var maxContent = _dropped > 0 ? _capacity - 1 : _capacity;
        while (_lines.Count > maxContent)
        {
            _lines.Dequeue();
            _dropped++;
            maxContent = _capacity - 1;
        }
    }

    public IReadOnlyList<ProcessOutputLine> Snapshot()
    {
        if (_dropped == 0)
            return [.. _lines];

        return [new ProcessOutputLine(TruncationMarker(_dropped), ProcessOutputStream.Stdout), .. _lines];
    }

    public void Clear()
    {
        _lines.Clear();
        _dropped = 0;
    }

    public static bool IsTruncationMarker(string text)
        => text.StartsWith(TruncationPrefix, StringComparison.Ordinal);

    public static string TruncationMarker(int dropped)
        => dropped == 1
            ? "[truncated] 1 earlier line was dropped."
            : $"[truncated] {dropped} earlier lines were dropped.";
}
