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
    private bool _isBusy;
    private bool _isApplyingHead;
    private bool _isCreatingBranch;
    private string _branch = string.Empty;
    private string _newBranchName = string.Empty;
    private string _commitMessage = string.Empty;
    private string? _errorMessage;
    private string? _statusMessage;
    private int _selectedFileCount;
    private bool _isConfirmingDiscard;
    private string? _discardConfirmMessage;
    private CancellationTokenSource? _watch;

    public ObservableCollection<GitChangeNodeViewModel> Nodes { get; } = [];
    public ObservableCollection<string> LocalBranches { get; } = [];

    public GitFileDiffViewModel Diff { get; } = new();

    public GitPanelViewModel(GitController git)
    {
        _git = git;
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        ToggleCommand = ReactiveCommand.Create(() => { IsVisible = !IsVisible; });
        DiscardCommand = ReactiveCommand.Create(
            RequestDiscardConfirm,
            this.WhenAnyValue(
                x => x.SelectedFileCount,
                x => x.IsBusy,
                x => x.IsConfirmingDiscard,
                (selected, busy, confirming) => selected > 0 && !busy && !confirming));
        ConfirmDiscardCommand = ReactiveCommand.CreateFromTask(
            ConfirmDiscardAsync,
            this.WhenAnyValue(x => x.IsConfirmingDiscard, x => x.IsBusy, (confirming, busy) => confirming && !busy));
        CancelDiscardCommand = ReactiveCommand.Create(CancelDiscardConfirm);
        BeginCreateBranchCommand = ReactiveCommand.Create(() => { IsCreatingBranch = true; });
        CancelCreateBranchCommand = ReactiveCommand.Create(() =>
        {
            IsCreatingBranch = false;
            NewBranchName = string.Empty;
        });
        CreateBranchCommand = ReactiveCommand.CreateFromTask(
            CreateBranchAsync,
            this.WhenAnyValue(x => x.NewBranchName, x => x.IsBusy, (name, busy) => !string.IsNullOrWhiteSpace(name) && !busy));
        SwitchBranchCommand = ReactiveCommand.CreateFromTask<string>(
            SwitchToBranchAsync,
            this.WhenAnyValue(x => x.IsBusy, busy => !busy));
        CommitCommand = ReactiveCommand.CreateFromTask(
            CommitAsync,
            this.WhenAnyValue(
                x => x.SelectedFileCount,
                x => x.CommitMessage,
                x => x.IsBusy,
                (selected, message, busy) => selected > 0 && !string.IsNullOrWhiteSpace(message) && !busy));
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isVisible, value);
            this.RaisePropertyChanged(nameof(ToggleIcon));
            if (value)
                StartWatching();
            else
                StopWatching();
        }
    }

    public string ToggleIcon => _isVisible ? "›" : "‹";

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public string Branch
    {
        get => _branch;
        set
        {
            if (_isApplyingHead)
            {
                this.RaiseAndSetIfChanged(ref _branch, value);
                return;
            }

            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, _branch, StringComparison.Ordinal) || IsBusy)
                return;

            _ = SwitchToBranchAsync(value);
        }
    }

    public bool IsCreatingBranch
    {
        get => _isCreatingBranch;
        private set => this.RaiseAndSetIfChanged(ref _isCreatingBranch, value);
    }

    public string NewBranchName
    {
        get => _newBranchName;
        set => this.RaiseAndSetIfChanged(ref _newBranchName, value);
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

    public bool IsConfirmingDiscard
    {
        get => _isConfirmingDiscard;
        private set => this.RaiseAndSetIfChanged(ref _isConfirmingDiscard, value);
    }

    public string? DiscardConfirmMessage
    {
        get => _discardConfirmMessage;
        private set => this.RaiseAndSetIfChanged(ref _discardConfirmMessage, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public ReactiveCommand<Unit, Unit> DiscardCommand { get; }
    public ReactiveCommand<Unit, Unit> ConfirmDiscardCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelDiscardCommand { get; }
    public ReactiveCommand<Unit, Unit> BeginCreateBranchCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCreateBranchCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateBranchCommand { get; }
    public ReactiveCommand<string, Unit> SwitchBranchCommand { get; }
    public ReactiveCommand<Unit, Unit> CommitCommand { get; }

    public void PrepareWorkspace(string? workspaceId, string? branch)
    {
        if (string.Equals(_workspaceId, workspaceId, StringComparison.Ordinal))
            return;

        _workspaceId = workspaceId;
        Nodes.Clear();
        SelectedFileCount = 0;
        CancelDiscardConfirm();
        Diff.Hide();
        if (workspaceId is null)
        {
            Clear();
            return;
        }

        ApplyHead(branch ?? string.Empty, string.IsNullOrWhiteSpace(branch) ? [] : [branch]);
        RaiseListProperties();
    }

    public async Task LoadAsync(string? workspaceId, CancellationToken cancellationToken = default, bool silent = false)
    {
        var request = ++_treeRequest;
        var sameWorkspace = string.Equals(_workspaceId, workspaceId, StringComparison.Ordinal);
        _workspaceId = workspaceId;
        if (!silent)
        {
            _diffRequest++;
            Diff.Hide();
        }
        if (workspaceId is null)
        {
            Clear();
            return;
        }

        if (!silent)
        {
            IsLoading = true;
            ErrorMessage = null;
        }
        try
        {
            var tree = await _git.GetChangesAsync(workspaceId, cancellationToken);
            if (request == _treeRequest)
            {
                if (silent)
                    ErrorMessage = null;
                ApplyTree(tree, preserveSelection: sameWorkspace);
            }
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            if (request != _treeRequest)
                return;

            if (silent)
            {
                ErrorMessage = $"Could not load Git changes: {ex.Message}";
                return;
            }

            Clear();
            ErrorMessage = $"Could not load Git changes: {ex.Message}";
        }
        finally
        {
            if (request == _treeRequest)
            {
                if (!silent)
                    IsLoading = false;
                RaiseListProperties();
            }
        }
    }

    public void Clear()
    {
        Nodes.Clear();
        LocalBranches.Clear();
        SetBranch(string.Empty);
        SelectedFileCount = 0;
        // A superseded request skips the loading reset in LoadAsync, so clearing the panel has to
        // settle it here; otherwise deselecting mid-load leaves the panel loading forever.
        IsLoading = false;
        ErrorMessage = null;
        StatusMessage = null;
        CancelDiscardConfirm();
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
        CancelDiscardConfirm();
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

        var files = SelectedFiles();
        IsBusy = true;
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
            await LoadAsync(_workspaceId, silent: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            ErrorMessage = $"Could not commit: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RequestDiscardConfirm()
    {
        var files = SelectedFiles();
        if (files.Count == 0)
            return;

        IsConfirmingDiscard = true;
        DiscardConfirmMessage = files.Count == 1
            ? $"Discard {files[0]}? This cannot be undone."
            : $"Discard {files.Count} files?\n{string.Join('\n', files)}\nThis cannot be undone.";
    }

    private void CancelDiscardConfirm()
    {
        IsConfirmingDiscard = false;
        DiscardConfirmMessage = null;
    }

    private Task ConfirmDiscardAsync()
    {
        CancelDiscardConfirm();
        return DiscardAsync();
    }

    private async Task DiscardAsync()
    {
        if (_workspaceId is null)
            return;

        var files = SelectedFiles();
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            var result = await _git.DiscardAsync(_workspaceId, files);
            if (!result.Succeeded)
            {
                ErrorMessage = result.Error ?? "The discard failed.";
                return;
            }

            StatusMessage = $"Discarded {files.Count} file(s).";
            await LoadAsync(_workspaceId, silent: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            ErrorMessage = $"Could not discard: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task CreateBranchAsync() => SwitchToBranchAsync(NewBranchName, create: true);

    private Task SwitchToBranchAsync(string name) => SwitchToBranchAsync(name, create: false);

    private async Task SwitchToBranchAsync(string name, bool create)
    {
        if (_workspaceId is null || string.IsNullOrWhiteSpace(name) || name == Branch)
            return;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            var result = await _git.SwitchBranchAsync(_workspaceId, name.Trim(), create);
            if (!result.Succeeded)
            {
                ErrorMessage = result.Error ?? "The branch switch failed.";
                return;
            }

            if (create)
            {
                NewBranchName = string.Empty;
                IsCreatingBranch = false;
            }
            StatusMessage = create ? $"Created {name.Trim()}." : $"Switched to {name.Trim()}.";
            await LoadAsync(_workspaceId, silent: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            ErrorMessage = $"Could not switch branch: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void StartWatching()
    {
        StopWatching();
        _watch = new CancellationTokenSource();
        _ = WatchAsync(_watch.Token);
    }

    private void StopWatching()
    {
        _watch?.Cancel();
        _watch?.Dispose();
        _watch = null;
    }

    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(_workspaceId, cancellationToken, silent: true);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2.5), cancellationToken);
                await LoadAsync(_workspaceId, cancellationToken, silent: true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private void ApplyTree(GitChangeTreeDto? tree, bool preserveSelection)
    {
        var selected = preserveSelection
            ? Nodes.Where(node => node.IsFile && node.IsSelected).Select(node => node.Path).ToHashSet(StringComparer.Ordinal)
            : [];
        Nodes.Clear();
        SelectedFileCount = 0;
        ApplyHead(tree?.Branch ?? string.Empty, tree?.LocalBranches);
        if (tree is null)
            return;

        var root = CreateNode("Changes", string.Empty, 0, isDirectory: true, status: string.Empty);
        Nodes.Add(root);
        AppendDirectory(tree.Root, depth: 1, isRoot: true, ancestors: [root]);
        if (root.Files.Count == 0)
        {
            Nodes.Clear();
            return;
        }

        if (selected.Count == 0)
            return;

        _isApplyingSelection = true;
        try
        {
            foreach (var node in Nodes.Where(candidate => candidate.IsFile && selected.Contains(candidate.Path)))
                node.SetSelectedSilently(true);
            foreach (var directory in Nodes.Where(candidate => candidate.IsDirectory))
                directory.SetSelectedSilently(directory.Files.Count > 0 && directory.Files.All(file => file.IsSelected));
        }
        finally
        {
            _isApplyingSelection = false;
        }

        SelectedFileCount = Nodes.Count(node => node.IsFile && node.IsSelected);
    }

    private void ApplyHead(string branch, IReadOnlyList<string>? branches)
    {
        _isApplyingHead = true;
        try
        {
            LocalBranches.Clear();
            foreach (var name in (branches ?? [])
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Distinct(StringComparer.Ordinal))
                LocalBranches.Add(name);

            if (!string.IsNullOrWhiteSpace(branch) && !LocalBranches.Contains(branch))
                LocalBranches.Insert(0, branch);

            Branch = branch;
        }
        finally
        {
            _isApplyingHead = false;
        }
    }

    private void SetBranch(string branch)
    {
        _isApplyingHead = true;
        try
        {
            Branch = branch;
        }
        finally
        {
            _isApplyingHead = false;
        }
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

    private List<string> SelectedFiles() =>
        Nodes.Where(node => node.IsFile && node.IsSelected).Select(node => node.Path).ToList();

    private static string ShortCommit(string? commit)
        => string.IsNullOrWhiteSpace(commit) ? "HEAD" : commit[..Math.Min(8, commit.Length)];
}
