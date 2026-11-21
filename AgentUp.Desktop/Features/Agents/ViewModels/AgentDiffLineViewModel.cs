using AgentUp.Desktop.Features.Agents.Models;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

/// <summary>One line of an edit's diff, highlighted in the language of the file it changed.</summary>
public sealed class AgentDiffLineViewModel(AgentDiffLine line, string? path)
{
    public string Number => (line.NewNumber ?? line.OldNumber)?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
    public string Prefix => line.Kind switch { "added" => "+", "deleted" => "-", _ => " " };
    public bool IsAdded => line.Kind == "added";
    public bool IsDeleted => line.Kind == "deleted";
    public bool IsHunk => line.Kind == "hunk";
    public IReadOnlyList<SyntaxToken> Tokens => IsHunk
        ? [new SyntaxToken("plain", line.Text)]
        : SyntaxHighlightProvider.Tokenize(line.Text, SyntaxHighlightProvider.DetectLanguage(path ?? ""));
}
