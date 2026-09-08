using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Git.Controllers;
using AgentUp.Desktop.Features.Git.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Git.ViewModels;

public sealed class GitPanelViewModel : ReactiveObject, IGitChangeNodeHost
{
    private readonly GitController _git;
    private bool _isApplyingSelection;
    // Monotonic ids for the in-flight tree and diff requests. A response is applied only when it
    // still belongs to the newest request, so a slow reply for a previous workspace or file cannot
    // repopulate the panel after the user moved on.
    private int _treeRequest;
    private int _diffRequest;
    private string? _workspaceId;
    private bool _isVisible;
    private bool _isLoading;
    private bool _isCommitting;
    private string _branch = string.Empty;
    private string _commitMessage = string.Empty;
    private string? _errorMessage;
    private string? _statusMessage;
    private int _selectedFileCount;

    public ObservableCollection<GitChangeNodeViewModel> Nodes { get; } = [];

    public GitFileDiffViewModel Diff { get; } = new();

    public GitPanelViewModel(GitController git)
    {
        _git = git;
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        ToggleCommand = ReactiveCommand.Create(() => { IsVisible = !IsVisible; });
        CommitCommand = ReactiveCommand.CreateFromTask(
            CommitAsync,
            this.WhenAnyValue(
                x => x.SelectedFileCount,
                x => x.CommitMessage,
                x => x.IsCommitting,
                (selected, message, committing) => selected > 0 && !string.IsNullOrWhiteSpace(message) && !committing));
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isVisible, value);
            this.RaisePropertyChanged(nameof(ToggleIcon));
        }
    }

    public string ToggleIcon => _isVisible ? "›" : "‹";

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool IsCommitting
    {
        get => _isCommitting;
        private set => this.RaiseAndSetIfChanged(ref _isCommitting, value);
    }

    public string Branch
    {
        get => _branch;
        private set => this.RaiseAndSetIfChanged(ref _branch, value);
    }

    public string CommitMessage
    {
        get => _commitMessage;
        set => this.RaiseAndSetIfChanged(ref _commitMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public int SelectedFileCount
    {
        get => _selectedFileCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _selectedFileCount, value);
            this.RaisePropertyChanged(nameof(SelectionSummary));
        }
    }

    public int FileCount => Nodes.Count(node => node.IsFile);

    public string SelectionSummary => $"{SelectedFileCount} of {FileCount} file(s) selected";

    public bool ShowEmptyState => Nodes.Count == 0 && !IsLoading && ErrorMessage is null;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public ReactiveCommand<Unit, Unit> CommitCommand { get; }

    public async Task LoadAsync(string? workspaceId, CancellationToken cancellationToken = default)
    {
        var request = ++_treeRequest;
        _diffRequest++;
        _workspaceId = workspaceId;
        Diff.Hide();
        if (workspaceId is null)
        {
            Clear();
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var tree = await _git.GetChangesAsync(workspaceId, cancellationToken);
            if (request == _treeRequest)
                ApplyTree(tree);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            if (request != _treeRequest)
                return;

            Clear();
            ErrorMessage = $"Could not load Git changes: {ex.Message}";
        }
        finally
        {
            if (request == _treeRequest)
            {
                IsLoading = false;
                RaiseListProperties();
            }
        }
    }

    public void Clear()
    {
        Nodes.Clear();
        Branch = string.Empty;
        SelectedFileCount = 0;
        ErrorMessage = null;
        StatusMessage = null;
        Diff.Hide();
        RaiseListProperties();
    }

    async Task IGitChangeNodeHost.OpenFileAsync(GitChangeNodeViewModel node)
    {
        var workspaceId = _workspaceId;
        if (workspaceId is null)
            return;

        var request = ++_diffRequest;
        Diff.ShowLoading(node.Path, node.Status);
        try
        {
            var diff = await _git.GetFileDiffAsync(workspaceId, node.Path);
            if (request == _diffRequest)
                ShowDiff(node, diff);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            if (request == _diffRequest)
                Diff.ShowError(node.Path, $"Could not load the diff: {ex.Message}");
        }
    }

    private void ShowDiff(GitChangeNodeViewModel node, GitFileDiffDto? diff)
    {
        if (diff is null)
            Diff.ShowError(node.Path, "This file no longer has changes.");
        else if (diff.IsBinary)
            Diff.ShowBinary(node.Path, diff.Status);
        else
            Diff.ShowDiff(node.Path, diff.Status, diff.Diff);
    }

    void IGitChangeNodeHost.NodeSelectionChanged(GitChangeNodeViewModel node)
    {
        if (_isApplyingSelection)
            return;

        _isApplyingSelection = true;
        try
        {
            ApplySelection(node);
        }
        finally
        {
            _isApplyingSelection = false;
        }

        SelectedFileCount = Nodes.Count(candidate => candidate.IsFile && candidate.IsSelected);
        StatusMessage = null;
    }

    private void ApplySelection(GitChangeNodeViewModel node)
    {
        foreach (var file in node.Files)
            file.SetSelectedSilently(node.IsSelected);

        foreach (var directory in Nodes.Where(candidate => candidate.IsDirectory))
            directory.SetSelectedSilently(directory.Files.Count > 0 && directory.Files.All(file => file.IsSelected));
    }

    private async Task RefreshAsync() => await LoadAsync(_workspaceId);

    private async Task CommitAsync()
    {
        if (_workspaceId is null)
            return;

        var files = Nodes.Where(node => node.IsFile && node.IsSelected).Select(node => node.Path).ToList();
        IsCommitting = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            var result = await _git.CommitAsync(_workspaceId, files, CommitMessage);
            if (!result.Succeeded)
            {
                ErrorMessage = result.Error ?? "The commit failed.";
                return;
            }

            CommitMessage = string.Empty;
            StatusMessage = $"Committed {files.Count} file(s) as {ShortCommit(result.Commit)}.";
            await LoadAsync(_workspaceId);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            ErrorMessage = $"Could not commit: {ex.Message}";
        }
        finally
        {
            IsCommitting = false;
        }
    }

    private void ApplyTree(GitChangeTreeDto? tree)
    {
        Nodes.Clear();
        SelectedFileCount = 0;
        Branch = tree?.Branch ?? string.Empty;
        if (tree is null)
            return;

        AppendDirectory(tree.Root, depth: 0, isRoot: true, ancestors: []);
    }

    private void AppendDirectory(
        GitChangeDirectoryDto directory,
        int depth,
        bool isRoot,
        IReadOnlyList<GitChangeNodeViewModel> ancestors)
    {
        var directoryAncestors = ancestors;
        if (!isRoot)
        {
            var node = CreateNode(directory.Name, directory.Path, depth, isDirectory: true, status: string.Empty);
            Nodes.Add(node);
            directoryAncestors = [.. ancestors, node];
        }

        var childDepth = isRoot ? depth : depth + 1;
        foreach (var child in directory.Directories)
            AppendDirectory(child, childDepth, isRoot: false, ancestors: directoryAncestors);

        foreach (var file in directory.Files)
        {
            var node = CreateNode(file.Name, file.Path, childDepth, isDirectory: false, status: file.Status);
            Nodes.Add(node);
            foreach (var ancestor in directoryAncestors)
                ancestor.AddFile(node);
        }
    }

    private GitChangeNodeViewModel CreateNode(string name, string path, int depth, bool isDirectory, string status)
    {
        var node = new GitChangeNodeViewModel(name, path, depth, isDirectory, status);
        node.SetHost(this);
        return node;
    }

    private void RaiseListProperties()
    {
        this.RaisePropertyChanged(nameof(FileCount));
        this.RaisePropertyChanged(nameof(SelectionSummary));
        this.RaisePropertyChanged(nameof(ShowEmptyState));
    }

    private static string ShortCommit(string? commit)
        => string.IsNullOrWhiteSpace(commit) ? "HEAD" : commit[..Math.Min(8, commit.Length)];
}
