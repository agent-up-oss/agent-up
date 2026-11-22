namespace AgentUp.Desktop.Features.Files.Providers;

/// <summary>
/// The arithmetic an editor page needs about its text: how many lines it has, where the caret is,
/// and how a size and a Git status read to a person.
/// </summary>
public static class WorkspaceFileTextProvider
{
    public static int LineCount(string? text)
        => string.IsNullOrEmpty(text) ? 1 : text.AsSpan().Count('\n') + 1;

    /// <summary>One number per line, for the gutter beside the editor.</summary>
    public static string LineNumbers(int lineCount)
        => string.Join('\n', Enumerable.Range(1, Math.Max(1, lineCount)));

    /// <summary>The one-based line and column of a caret index, counting a tab as one column.</summary>
    public static (int Line, int Column) CaretPosition(string? text, int caretIndex)
    {
        var value = text ?? string.Empty;
        var index = Math.Clamp(caretIndex, 0, value.Length);
        var before = value.AsSpan(0, index);
        var lastBreak = before.LastIndexOf('\n');
        var line = 1 + before.Count('\n');
        return (line, index - lastBreak);
    }

    public static string CaretLabel(string? text, int caretIndex)
    {
        var (line, column) = CaretPosition(text, caretIndex);
        return $"Ln {line}, Col {column}";
    }

    public static string SizeLabel(long? bytes) => bytes switch
    {
        null => string.Empty,
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes / (1024d * 1024d):0.#} MB",
    };

    /// <summary>The change-list glyph, so a file reads the same in Files as in Git.</summary>
    public static string StatusGlyph(string? status) => status switch
    {
        null or "" => string.Empty,
        "Added" => "+",
        "Untracked" => "?",
        "Deleted" => "−",
        "Renamed" => "→",
        "Conflicted" => "!",
        _ => "M",
    };

    public static string StatusLabel(string? status) => string.IsNullOrEmpty(status) ? "Unchanged" : status;
}
