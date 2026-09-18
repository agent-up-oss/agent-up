using System.Collections.ObjectModel;
using System.Reactive;
using System.Text.Json;
using AgentUp.Desktop.Features.Git.Controllers;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Providers;
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
    private bool _isConfirmingForcePush;
    private string? _discardConfirmMessage;
    private GitBranchChoiceDto? _selectedBranchItem;
    private int _ahead;
    private int _behind;
    private CancellationTokenSource? _watch;

    public ObservableCollection<GitChangeNodeViewModel> Nodes { get; } = [];
    public ObservableCollection<string> LocalBranches { get; } = [];
    public ObservableCollection<GitBranchChoiceDto> BranchItems { get; } = [];
    public ObservableCollection<GitLogRowDto> LogRows { get; } = [];
    public ObservableCollection<CommitQueueEntryDto> QueueEntries { get; } = [];

    public string? QueueWorktreePath { get; private set; }
    public long QueueGeneration { get; private set; }
    public bool HasQueuedProposals => QueueEntries.Count > 0;

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
        FetchCommand = ReactiveCommand.CreateFromTask(
            FetchAsync,
            this.WhenAnyValue(x => x.IsBusy, busy => !busy));
        PullCommand = ReactiveCommand.CreateFromTask(
            PullAsync,
            this.WhenAnyValue(x => x.IsBusy, busy => !busy));
        PushCommand = ReactiveCommand.CreateFromTask(
            () => PushAsync(forceWithLease: false),
            this.WhenAnyValue(x => x.IsBusy, x => x.IsConfirmingForcePush, (busy, confirming) => !busy && !confirming));
        RequestForcePushCommand = ReactiveCommand.Create(
            RequestForcePushConfirm,
            this.WhenAnyValue(x => x.IsBusy, x => x.IsConfirmingForcePush, (busy, confirming) => !busy && !confirming));
        ConfirmForcePushCommand = ReactiveCommand.CreateFromTask(
            ConfirmForcePushAsync,
            this.WhenAnyValue(x => x.IsConfirmingForcePush, x => x.IsBusy, (confirming, busy) => confirming && !busy));
        CancelForcePushCommand = ReactiveCommand.Create(CancelForcePushConfirm);
        CheckoutLogRefCommand = ReactiveCommand.CreateFromTask<string>(
            CheckoutFromLogAsync,
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

    public string SyncSummary => Ahead == 0 && Behind == 0
        ? string.Empty
        : $"↑{Ahead} ↓{Behind}";

    public bool ShowEmptyState => Nodes.Count == 0 && !IsLoading && ErrorMessage is null;

    public int Ahead
    {
        get => _ahead;
        private set
        {
            this.RaiseAndSetIfChanged(ref _ahead, value);
            this.RaisePropertyChanged(nameof(SyncSummary));
        }
    }

    public int Behind
    {
        get => _behind;
        private set
        {
            this.RaiseAndSetIfChanged(ref _behind, value);
            this.RaisePropertyChanged(nameof(SyncSummary));
        }
    }

    public GitBranchChoiceDto? SelectedBranchItem
    {
        get => _selectedBranchItem;
        set
        {
            if (_isApplyingHead)
            {
                this.RaiseAndSetIfChanged(ref _selectedBranchItem, value);
                return;
            }

            if (value is null || value.Kind == "header" || IsBusy)
                return;

            if (string.Equals(value.Kind, "remote", StringComparison.Ordinal))
            {
                _ = CheckoutRemoteAsync(value.Key);
                return;
            }

            if (string.Equals(value.Key, Branch, StringComparison.Ordinal))
            {
                this.RaiseAndSetIfChanged(ref _selectedBranchItem, value);
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedBranchItem, value);
            _ = SwitchToBranchAsync(value.Key);
        }
    }

    public bool IsConfirmingForcePush
    {
        get => _isConfirmingForcePush;
        private set => this.RaiseAndSetIfChanged(ref _isConfirmingForcePush, value);
    }

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
    public ReactiveCommand<Unit, Unit> FetchCommand { get; }
    public ReactiveCommand<Unit, Unit> PullCommand { get; }
    public ReactiveCommand<Unit, Unit> PushCommand { get; }
    public ReactiveCommand<Unit, Unit> RequestForcePushCommand { get; }
    public ReactiveCommand<Unit, Unit> ConfirmForcePushCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelForcePushCommand { get; }
    public ReactiveCommand<string, Unit> CheckoutLogRefCommand { get; }
    public ReactiveCommand<Unit, Unit> CommitCommand { get; }

    public void PrepareWorkspace(string? workspaceId, string? branch)
    {
        if (string.Equals(_workspaceId, workspaceId, StringComparison.Ordinal))
            return;

        _workspaceId = workspaceId;
        Nodes.Clear();
        ClearQueue();
        SelectedFileCount = 0;
        CancelDiscardConfirm();
        Diff.Hide();
        if (workspaceId is null)
        {
            Clear();
            return;
        }

        ApplyHead(branch ?? string.Empty, string.IsNullOrWhiteSpace(branch) ? [] : [branch], [], 0, 0);
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
            CommitQueueDto? queue = null;
            string? queueError = null;
            try
            {
                queue = await _git.GetCommitQueueAsync(workspaceId, cancellationToken);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException or TaskCanceledException)
            {
                queueError = ex.Message;
            }

            if (request == _treeRequest)
            {
                if (silent && queueError is null)
                    ErrorMessage = null;
                ApplyTree(tree, preserveSelection: sameWorkspace);
                ApplyQueue(queue);
                await ApplyLogAsync(workspaceId, cancellationToken, request);
                if (queueError is not null)
                    ErrorMessage = $"Could not load the agent proposal queue: {queueError}";
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

    internal void ApplyQueue(CommitQueueDto? queue)
    {
        QueueEntries.Clear();
        foreach (var entry in queue?.Entries ?? [])
            QueueEntries.Add(entry);
        QueueWorktreePath = queue?.QueueWorktreePath;
        QueueGeneration = queue?.Generation ?? 0;
        this.RaisePropertyChanged(nameof(QueueWorktreePath));
        this.RaisePropertyChanged(nameof(QueueGeneration));
        this.RaisePropertyChanged(nameof(HasQueuedProposals));
    }

    private void ClearQueue() => ApplyQueue(null);

    public void Clear()
    {
        Nodes.Clear();
        ClearQueue();
        LocalBranches.Clear();
        BranchItems.Clear();
        LogRows.Clear();
        Ahead = 0;
        Behind = 0;
        SetBranch(string.Empty);
        SelectedFileCount = 0;
        // A superseded request skips the loading reset in LoadAsync, so clearing the panel has to
        // settle it here; otherwise deselecting mid-load leaves the panel loading forever.
        IsLoading = false;
        ErrorMessage = null;
        StatusMessage = null;
        CancelDiscardConfirm();
        CancelForcePushConfirm();
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

    private async Task CheckoutRemoteAsync(string name)
    {
        if (_workspaceId is null || string.IsNullOrWhiteSpace(name) || IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            var result = await _git.CheckoutRemoteAsync(_workspaceId, name.Trim());
            if (!result.Succeeded)
            {
                ErrorMessage = result.Error ?? "The checkout failed.";
                return;
            }

            StatusMessage = $"Checked out {name.Trim()}.";
            await LoadAsync(_workspaceId, silent: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            ErrorMessage = $"Could not check out: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task CheckoutFromLogAsync(string name) =>
        LocalBranches.Contains(name)
            ? SwitchToBranchAsync(name, create: false)
            : CheckoutRemoteAsync(name);

    private Task FetchAsync() => RunSyncAsync("Fetched.", (workspaceId, ct) => _git.FetchAsync(workspaceId, ct));

    private Task PullAsync() => RunSyncAsync("Pulled.", (workspaceId, ct) => _git.PullAsync(workspaceId, cancellationToken: ct));

    private Task PushAsync(bool forceWithLease) =>
        RunSyncAsync(
            forceWithLease ? "Force-pushed with lease." : "Pushed.",
            (workspaceId, ct) => _git.PushAsync(workspaceId, forceWithLease, setUpstream: false, ct));

    private void RequestForcePushConfirm() => IsConfirmingForcePush = true;

    private void CancelForcePushConfirm() => IsConfirmingForcePush = false;

    private async Task ConfirmForcePushAsync()
    {
        CancelForcePushConfirm();
        await PushAsync(forceWithLease: true);
    }

    private async Task RunSyncAsync(string success, Func<string, CancellationToken, Task<GitSyncResultDto>> action)
    {
        if (_workspaceId is null)
            return;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            var result = await action(_workspaceId, CancellationToken.None);
            if (!result.Succeeded)
            {
                ErrorMessage = result.Error ?? "The Git remote operation failed.";
                return;
            }

            StatusMessage = success;
            await LoadAsync(_workspaceId, silent: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            ErrorMessage = $"Could not update remotes: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyLogAsync(string workspaceId, CancellationToken cancellationToken, int request)
    {
        try
        {
            var log = await _git.GetLogAsync(workspaceId, cancellationToken);
            if (request != _treeRequest)
                return;
            LogRows.Clear();
            foreach (var row in GitLogLayoutProvider.Layout(log?.Commits))
                LogRows.Add(row);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException or TaskCanceledException)
        {
            if (request != _treeRequest || cancellationToken.IsCancellationRequested)
                return;
            LogRows.Clear();
            if (ErrorMessage is null)
                ErrorMessage = $"Could not load Git history: {ex.Message}";
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
        ApplyHead(tree);
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

    private void ApplyHead(GitChangeTreeDto? tree) =>
        ApplyHead(
            tree?.Branch ?? string.Empty,
            tree?.LocalBranches,
            tree?.RemoteBranches,
            tree?.Ahead ?? 0,
            tree?.Behind ?? 0);

    private void ApplyHead(
        string branch,
        IReadOnlyList<string>? branches,
        IReadOnlyList<GitRemoteBranchDto>? remotes,
        int ahead,
        int behind)
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

            Ahead = ahead;
            Behind = behind;
            RebuildBranchItems(branch, remotes);
            Branch = branch;
        }
        finally
        {
            _isApplyingHead = false;
        }
    }

    private void RebuildBranchItems(string branch, IReadOnlyList<GitRemoteBranchDto>? remotes)
    {
        BranchItems.Clear();
        if (LocalBranches.Count > 0)
        {
            BranchItems.Add(new GitBranchChoiceDto("header-local", "Local", "header"));
            foreach (var name in LocalBranches)
                BranchItems.Add(new GitBranchChoiceDto(name, name, "local"));
        }

        var remoteBranches = (remotes ?? [])
            .Where(remote => !string.IsNullOrWhiteSpace(remote.Name))
            .GroupBy(remote => $"{remote.Remote}/{remote.Name}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (remoteBranches.Count > 0)
        {
            BranchItems.Add(new GitBranchChoiceDto("header-remote", "Remote", "header"));
            foreach (var remote in remoteBranches)
            {
                var key = $"{remote.Remote}/{remote.Name}";
                BranchItems.Add(new GitBranchChoiceDto(key, key, "remote"));
            }
        }

        SelectedBranchItem = BranchItems.FirstOrDefault(item =>
            item.Kind == "local" && string.Equals(item.Key, branch, StringComparison.Ordinal));
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
