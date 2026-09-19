using AgentUp.Desktop.Features.Git.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Git.ViewModels;

public sealed class GitFileDiffLineViewModel : ReactiveObject
{
    private readonly string _path;
    private IReadOnlyList<GitFileDiffTokenViewModel>? _tokens;
    private bool _isCurrent;

    public GitFileDiffLineViewModel(string path, GitFileViewerLine line)
    {
        _path = path;
        Index = line.Index;
        Kind = line.Kind;
        OldNumber = line.OldNumber;
        NewNumber = line.NewNumber;
        Prefix = line.Prefix;
        Text = line.Text;
        Gutter = (line.NewNumber ?? line.OldNumber)?.ToString() ?? string.Empty;
        IsAdded = line.Kind == "added";
        IsDeleted = line.Kind == "deleted";
        IsHunk = line.Kind == "hunk";
        IsMeta = line.Kind == "meta";
    }

    public int Index { get; }
    public string Kind { get; }
    public int? OldNumber { get; }
    public int? NewNumber { get; }
    public string Prefix { get; }
    public string Text { get; }
    public string Gutter { get; }
    public bool IsAdded { get; }
    public bool IsDeleted { get; }
    public bool IsHunk { get; }
    public bool IsMeta { get; }

    public bool IsCurrent
    {
        get => _isCurrent;
        set => this.RaiseAndSetIfChanged(ref _isCurrent, value);
    }

    public IReadOnlyList<GitFileDiffTokenViewModel> Tokens
        => _tokens ??= GitFileViewerProvider.Highlight(_path, new GitFileViewerLine(Index, Kind, OldNumber, NewNumber, Prefix, Text))
            .Select(token => new GitFileDiffTokenViewModel(token.Kind, token.Text))
            .ToList();
}
