using System.Reactive;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Git.ViewModels;

public sealed class GitChangeNodeViewModel : ReactiveObject
{
    private readonly List<GitChangeNodeViewModel> _files = [];
    private IGitChangeNodeHost? _host;
    private bool _isSelected;
    private bool _isOpen;
    private bool _isExpanded = true;
    private bool _isRowVisible = true;

    public string Name { get; }
    public string Path { get; }
    public int Depth { get; }
    public bool IsDirectory { get; }
    public string Status { get; }
    public IReadOnlyList<int> Guides { get; }

    public bool IsFile => !IsDirectory;
    public string Glyph => IsDirectory ? ToggleGlyph : StatusGlyph(Status);
    public string ToggleGlyph => IsExpanded ? "▾" : "▸";
    public string ToolTip => IsDirectory ? Path : $"{Path} — {Status}";

    public bool IsAdded => Status == "Added";
    public bool IsUntracked => Status == "Untracked";
    public bool IsDeleted => Status == "Deleted";
    public bool IsRenamed => Status == "Renamed";
    public bool IsConflicted => Status == "Conflicted";
    public bool IsModified => IsFile && !IsAdded && !IsUntracked && !IsDeleted && !IsRenamed && !IsConflicted;
    public bool IsCollapsed => IsDirectory && !IsExpanded;

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

    public bool IsOpen
    {
        get => _isOpen;
        private set => this.RaiseAndSetIfChanged(ref _isOpen, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        private set
        {
            if (_isExpanded == value)
                return;

            this.RaiseAndSetIfChanged(ref _isExpanded, value);
            this.RaisePropertyChanged(nameof(IsCollapsed));
            this.RaisePropertyChanged(nameof(ToggleGlyph));
            this.RaisePropertyChanged(nameof(Glyph));
        }
    }

    public bool IsRowVisible
    {
        get => _isRowVisible;
        private set => this.RaiseAndSetIfChanged(ref _isRowVisible, value);
    }

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleExpandCommand { get; }

    public GitChangeNodeViewModel(string name, string path, int depth, bool isDirectory, string status)
    {
        Name = name;
        Path = path;
        Depth = depth;
        IsDirectory = isDirectory;
        Status = status;
        Guides = depth > 0 ? [.. Enumerable.Range(0, depth)] : [];
        OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync);
        ToggleExpandCommand = ReactiveCommand.Create(ToggleExpand);
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

    internal void SetOpenSilently(bool open) => IsOpen = open;

    internal void SetExpandedSilently(bool expanded) => IsExpanded = expanded;

    internal void SetRowVisibleSilently(bool visible) => IsRowVisible = visible;

    private void ToggleExpand()
    {
        if (!IsDirectory)
            return;

        IsExpanded = !IsExpanded;
        _host?.NodeExpansionChanged(this);
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
}
