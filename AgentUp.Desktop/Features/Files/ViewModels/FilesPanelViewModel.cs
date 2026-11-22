using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using AgentUp.Desktop.Features.Files.Controllers;
using AgentUp.Desktop.Features.Files.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Files.ViewModels;

/// <summary>
/// The Files tab: the whole clone as a tree, folders folded until opened, and one file opened as
/// a page in the tab's place. Nothing is fetched ahead of the reader: the top level is read when
/// the tab is first shown, and each folder when it is first unfolded. Rows keep the Git glyph of
/// a changed file and the changed-file count of a folder, so the tree says where the work is.
/// </summary>
public sealed class FilesPanelViewModel : ReactiveObject, IFileNodeHost
{
    private readonly WorkspaceFilesController _files;
    private string? _workspaceId;
    private string? _connectionId;
    private string _rootLabel = string.Empty;
    private bool _isVisible;
    private bool _loaded;
    private bool _isLoading;
    private string? _errorMessage;
    private bool _canWriteGit = true;
    private long _generation;

    public FilesPanelViewModel(WorkspaceFilesController files)
    {
        _files = files;
        Editor = new FileEditorViewModel(files);
        RefreshCommand = ReactiveCommand.CreateFromTask(() => LoadAsync());
        Editor.WhenAnyValue(editor => editor.IsOpen)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsEditorOpen)));
        Editor.WhenAnyValue(editor => editor.IsOpen)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsTreeVisible)));
        Editor.Closed.Subscribe(_ => ClearOpen());
    }

    /// <summary>Visible rows, in order: a folder's rows follow it while it is unfolded.</summary>
    public ObservableCollection<FileNodeViewModel> Nodes { get; } = [];

    public FileEditorViewModel Editor { get; }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public bool IsEditorOpen => Editor.IsOpen;

    public bool IsTreeVisible => !Editor.IsOpen;

    /// <summary>Which checkout this is, beside the Files label.</summary>
    public string RootLabel
    {
        get => _rootLabel;
        private set => this.RaiseAndSetIfChanged(ref _rootLabel, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(ShowEmptyState));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool ShowEmptyState => _loaded && !IsLoading && ErrorMessage is null && Nodes.Count == 0;

    /// <summary>False when the signed-in operation permissions do not include Git writes; the editor is then read only.</summary>
    public bool CanWriteGit
    {
        get => _canWriteGit;
        set
        {
            this.RaiseAndSetIfChanged(ref _canWriteGit, value);
            Editor.CanWriteGit = value;
        }
    }

    /// <summary>True while the tab is on screen. The first time it is, the top level is read.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isVisible, value);
            if (value && !_loaded && !IsLoading && _workspaceId is not null)
                _ = LoadAsync();
        }
    }

    /// <summary>
    /// Points the tab at a workspace. The same workspace on the same Server keeps what is open;
    /// anything else starts over, so two Servers that both have a workspace called main never share
    /// a tree.
    /// </summary>
    public void PrepareWorkspace(string? workspaceId, string? label, string? connectionId)
    {
        RootLabel = label ?? string.Empty;
        if (workspaceId == _workspaceId && connectionId == _connectionId)
            return;
        _workspaceId = workspaceId;
        _connectionId = connectionId;
        _generation++;
        _loaded = false;
        Nodes.Clear();
        ErrorMessage = null;
        IsLoading = false;
        if (Editor.IsOpen)
            Editor.Close();
        if (IsVisible && workspaceId is not null)
            _ = LoadAsync();
    }

    /// <summary>Reads the top level again, then every folder that is still unfolded, keeping the open file.</summary>
    public async Task LoadAsync()
    {
        var workspaceId = _workspaceId;
        if (workspaceId is null)
            return;
        var generation = ++_generation;
        var expanded = Nodes.Where(node => node.IsDirectory && node.IsExpanded).Select(node => node.Path).ToHashSet(StringComparer.Ordinal);
        var open = Editor.IsOpen ? Editor.Path : null;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var root = await _files.GetDirectoryAsync(workspaceId, string.Empty);
            if (generation != _generation)
                return;
            if (root is null)
            {
                Nodes.Clear();
                ErrorMessage = "This workspace is no longer on the Server.";
                return;
            }

            var top = Rows(root, 0);
            await ExpandAgainAsync(workspaceId, top, expanded, generation);
            if (generation != _generation)
                return;
            Nodes.Clear();
            foreach (var row in Visible(top))
                Nodes.Add(row);
            foreach (var node in Nodes.Where(node => node.Path == open))
                node.IsOpen = true;
            _loaded = true;
        }
        catch (HttpRequestException ex)
        {
            if (generation == _generation)
                ErrorMessage = ex.Message;
        }
        finally
        {
            if (generation == _generation)
                IsLoading = false;
            this.RaisePropertyChanged(nameof(ShowEmptyState));
        }
    }

    async Task IFileNodeHost.ToggleAsync(FileNodeViewModel node)
    {
        if (!node.IsDirectory || _workspaceId is not { } workspaceId)
            return;
        if (node.IsExpanded)
        {
            node.SetExpanded(false);
            RemoveDescendants(node);
            return;
        }

        if (node.Children is null)
        {
            var generation = _generation;
            node.IsLoading = true;
            try
            {
                var listing = await _files.GetDirectoryAsync(workspaceId, node.Path);
                if (generation != _generation)
                    return;
                node.SetChildren(listing is null ? [] : Rows(listing, node.Depth + 1));
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = ex.Message;
                return;
            }
            finally
            {
                node.IsLoading = false;
            }
        }

        node.SetExpanded(true);
        var index = Nodes.IndexOf(node);
        if (index < 0)
            return;
        foreach (var row in Visible(node.Children!))
            Nodes.Insert(++index, row);
    }

    Task IFileNodeHost.OpenFileAsync(FileNodeViewModel node)
    {
        if (_workspaceId is not { } workspaceId)
            return Task.CompletedTask;
        ClearOpen();
        node.IsOpen = true;
        return Editor.OpenAsync(workspaceId, node.Path);
    }

    private void ClearOpen()
    {
        foreach (var node in Nodes.Where(node => node.IsOpen))
            node.IsOpen = false;
    }

    private async Task ExpandAgainAsync(string workspaceId, IReadOnlyList<FileNodeViewModel> rows, HashSet<string> expanded, long generation)
    {
        foreach (var row in rows.Where(row => row.IsDirectory && expanded.Contains(row.Path)))
        {
            var listing = await _files.GetDirectoryAsync(workspaceId, row.Path);
            if (generation != _generation || listing is null)
                continue;
            var children = Rows(listing, row.Depth + 1);
            row.SetChildren(children);
            row.SetExpanded(true);
            await ExpandAgainAsync(workspaceId, children, expanded, generation);
        }
    }

    private List<FileNodeViewModel> Rows(WorkspaceDirectoryListingDto listing, int depth)
    {
        var rows = listing.Entries.Select(entry => new FileNodeViewModel(entry, depth, this)).ToList();
        if (listing.Truncated)
            rows.Add(FileNodeViewModel.Notice($"Showing the first {listing.Entries.Count} entries of this folder.", depth));
        return rows;
    }

    private static IEnumerable<FileNodeViewModel> Visible(IEnumerable<FileNodeViewModel> rows)
        => rows.SelectMany(row => row is { IsExpanded: true, Children: { } children }
            ? Visible(children).Prepend(row)
            : [row]);

    private void RemoveDescendants(FileNodeViewModel node)
    {
        var index = Nodes.IndexOf(node);
        if (index < 0)
            return;
        while (index + 1 < Nodes.Count && Nodes[index + 1].Depth > node.Depth)
            Nodes.RemoveAt(index + 1);
    }
}
