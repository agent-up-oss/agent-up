using System.Reactive;
using AgentUp.Desktop.Features.Files.DTOs;
using AgentUp.Desktop.Features.Files.Providers;
using AgentUp.Desktop.Shared.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Files.ViewModels;

/// <summary>
/// One row of the Files tree: a folder, a file, or a note under a folder the Server cut short.
/// A folder holds its children once it has been unfolded, so folding and unfolding again does not
/// ask the Server twice; Reload is what reads it again.
/// </summary>
public sealed class FileNodeViewModel : ReactiveObject
{
    private readonly IFileNodeHost? _host;
    private bool _isExpanded;
    private bool _isLoading;
    private bool _isOpen;

    internal FileNodeViewModel(WorkspaceFileEntryDto entry, int depth, IFileNodeHost? host)
    {
        _host = host;
        Name = entry.Name;
        Path = entry.Path;
        Depth = depth;
        IsDirectory = entry.IsDirectory;
        Status = entry.Status;
        ChangedFiles = entry.ChangedFiles;
        SizeLabel = IsDirectory ? string.Empty : WorkspaceFileTextProvider.SizeLabel(entry.Size);
        Guides = depth > 0 ? [.. Enumerable.Range(0, depth)] : [];
        ToggleCommand = ReactiveCommand.CreateFromTask(() => _host?.ToggleAsync(this) ?? Task.CompletedTask);
        // A folder's name unfolds it the way its chevron does; a file's name opens its page.
        OpenCommand = ReactiveCommand.CreateFromTask(() => _host is null
            ? Task.CompletedTask
            : IsDirectory ? _host.ToggleAsync(this) : _host.OpenFileAsync(this));
    }

    private FileNodeViewModel(string notice, int depth)
    {
        Name = notice;
        Path = string.Empty;
        Depth = depth;
        IsNotice = true;
        SizeLabel = string.Empty;
        Guides = depth > 0 ? [.. Enumerable.Range(0, depth)] : [];
        ToggleCommand = ReactiveCommand.Create(() => { });
        OpenCommand = ReactiveCommand.Create(() => { });
    }

    /// <summary>A muted line under a folder the Server listed only in part.</summary>
    public static FileNodeViewModel Notice(string text, int depth) => new(text, depth);

    public string Name { get; }
    public string Path { get; }
    public int Depth { get; }
    public bool IsDirectory { get; }
    public bool IsNotice { get; }
    public bool IsFile => !IsDirectory && !IsNotice;
    public string? Status { get; }
    public int ChangedFiles { get; }
    public string SizeLabel { get; }
    public IReadOnlyList<int> Guides { get; }

    /// <summary>The children the Server listed, or null until the folder is first unfolded.</summary>
    public IReadOnlyList<FileNodeViewModel>? Children { get; private set; }

    public bool HasChanges => IsDirectory && ChangedFiles > 0;
    public string ChangedLabel => ChangedFiles.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string StatusGlyph => WorkspaceFileTextProvider.StatusGlyph(Status);
    public bool HasStatus => IsFile && !string.IsNullOrEmpty(Status);
    public bool IsAdded => Status == "Added";
    public bool IsUntracked => Status == "Untracked";
    public bool IsRenamed => Status == "Renamed";
    public bool IsConflicted => Status == "Conflicted";
    public bool IsModified => HasStatus && !IsAdded && !IsUntracked && !IsRenamed && !IsConflicted;
    public string ToggleGlyph => IsExpanded ? "▾" : "▸";
    public string ToolTip => IsDirectory
        ? (HasChanges ? $"{Path} — {ChangedFiles} changed" : Path)
        : (HasStatus ? $"{Path} — {Status}" : Path);
    public string ToggleAutomationId => DesktopAutomationIds.FilesToggle(Path);
    public string OpenAutomationId => DesktopAutomationIds.FilesOpen(Path);

    public bool IsExpanded
    {
        get => _isExpanded;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isExpanded, value);
            this.RaisePropertyChanged(nameof(ToggleGlyph));
            this.RaisePropertyChanged(nameof(IsCollapsed));
        }
    }

    public bool IsCollapsed => IsDirectory && !IsExpanded;

    public bool IsLoading
    {
        get => _isLoading;
        internal set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>The file whose page is open, which the tree shows as the primary selection.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        internal set => this.RaiseAndSetIfChanged(ref _isOpen, value);
    }

    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    internal void SetChildren(IReadOnlyList<FileNodeViewModel> children) => Children = children;

    internal void SetExpanded(bool expanded) => IsExpanded = expanded;
}
