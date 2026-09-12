using AgentUp.Desktop.Shared.Models;
using System.Reactive;
using Avalonia;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Git.ViewModels;

public sealed class GitChangeNodeViewModel : ReactiveObject
{
    private const double IndentPerLevel = 14;

    private readonly List<GitChangeNodeViewModel> _files = [];
    private IGitChangeNodeHost? _host;
    private bool _isSelected;

    public string Name { get; }
    public string Path { get; }
    public int Depth { get; }
    public bool IsDirectory { get; }
    public string Status { get; }

    public bool IsFile => !IsDirectory;
    public Thickness Indent => new(Depth * IndentPerLevel, 0, 0, 0);
    public string Glyph => IsDirectory ? "▸" : StatusGlyph(Status);
    public string GlyphColor => IsDirectory ? AgentUpThemeColors.TextMuted : StatusColor(Status);
    public string NameColor => IsDirectory ? AgentUpThemeColors.TextMuted : AgentUpThemeColors.TextPrimary;
    public string ToolTip => IsDirectory ? Path : $"{Path} — {Status}";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            this.RaiseAndSetIfChanged(ref _isSelected, value);
            _host?.NodeSelectionChanged(this);
        }
    }

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    public GitChangeNodeViewModel(string name, string path, int depth, bool isDirectory, string status)
    {
        Name = name;
        Path = path;
        Depth = depth;
        IsDirectory = isDirectory;
        Status = status;
        OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync);
    }

    // Files owned by this node: a file owns itself, a directory owns every file beneath it.
    public IReadOnlyList<GitChangeNodeViewModel> Files => IsDirectory ? _files : [this];

    internal void SetHost(IGitChangeNodeHost host) => _host = host;

    internal void AddFile(GitChangeNodeViewModel file) => _files.Add(file);

    // Applies a selection change made elsewhere (a parent directory or a recomputed ancestor)
    // without re-entering host propagation.
    internal void SetSelectedSilently(bool selected)
    {
        if (_isSelected == selected)
            return;

        _isSelected = selected;
        this.RaisePropertyChanged(nameof(IsSelected));
    }

    private Task OpenAsync()
        => _host is null || IsDirectory ? Task.CompletedTask : _host.OpenFileAsync(this);

    private static string StatusGlyph(string status) => status switch
    {
        "Added" => "+",
        "Untracked" => "?",
        "Deleted" => "−",
        "Renamed" => "→",
        "Conflicted" => "!",
        _ => "M"
    };

    private static string StatusColor(string status) => status switch
    {
        "Added" or "Untracked" => AgentUpThemeColors.AccentSoft,
        "Deleted" => AgentUpThemeColors.StatusDanger,
        "Renamed" => AgentUpThemeColors.StatusInfo,
        "Conflicted" => AgentUpThemeColors.StatusWarning,
        _ => AgentUpThemeColors.TextSecondary
    };
}
