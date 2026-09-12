using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceOverviewViewModel : ReactiveObject
{
    private readonly WorkspacesController _workspaces;
    private int _loadRequest;
    private string? _workspaceId;
    private bool _showSkeleton;
    private string _displayName = string.Empty;
    private string _state = string.Empty;
    private string _repositoryPath = string.Empty;
    private string _worktreePath = string.Empty;
    private string _commit = string.Empty;
    private string _cpu = "—";
    private string _memory = "—";
    private string _storage = "—";
    private string _processCount = "—";
    private string _applicationCount = "—";
    private string? _errorMessage;
    private bool _isLoading;

    public WorkspaceOverviewViewModel(WorkspacesController workspaces)
    {
        _workspaces = workspaces;
    }

    public string DisplayName
    {
        get => _displayName;
        private set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    public string State
    {
        get => _state;
        private set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    public string RepositoryPath
    {
        get => _repositoryPath;
        private set => this.RaiseAndSetIfChanged(ref _repositoryPath, value);
    }

    public string WorktreePath
    {
        get => _worktreePath;
        private set => this.RaiseAndSetIfChanged(ref _worktreePath, value);
    }

    public string Commit
    {
        get => _commit;
        private set => this.RaiseAndSetIfChanged(ref _commit, value);
    }

    public string Cpu
    {
        get => _cpu;
        private set => this.RaiseAndSetIfChanged(ref _cpu, value);
    }

    public string Memory
    {
        get => _memory;
        private set => this.RaiseAndSetIfChanged(ref _memory, value);
    }

    public string Storage
    {
        get => _storage;
        private set => this.RaiseAndSetIfChanged(ref _storage, value);
    }

    public string ProcessCount
    {
        get => _processCount;
        private set => this.RaiseAndSetIfChanged(ref _processCount, value);
    }

    public string ApplicationCount
    {
        get => _applicationCount;
        private set => this.RaiseAndSetIfChanged(ref _applicationCount, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool ShowSkeleton
    {
        get => _showSkeleton;
        private set => this.RaiseAndSetIfChanged(ref _showSkeleton, value);
    }

    public async Task LoadAsync(string? workspaceId, CancellationToken cancellationToken = default)
    {
        var request = ++_loadRequest;
        if (workspaceId is null)
        {
            Clear();
            return;
        }

        var switched = !string.Equals(_workspaceId, workspaceId, StringComparison.Ordinal);
        _workspaceId = workspaceId;
        ErrorMessage = null;
        if (switched)
        {
            ResetDisplay();
            ShowSkeleton = true;
        }

        IsLoading = true;
        try
        {
            var overview = await _workspaces.GetOverviewAsync(workspaceId, cancellationToken);
            if (request != _loadRequest)
                return;

            if (overview is null)
            {
                ResetDisplay();
                ErrorMessage = "Could not load workspace overview.";
                ShowSkeleton = false;
                return;
            }

            Apply(overview);
            ShowSkeleton = false;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            if (request != _loadRequest)
                return;

            ErrorMessage = $"Could not load workspace overview: {ex.Message}";
            ShowSkeleton = false;
        }
        finally
        {
            if (request == _loadRequest)
                IsLoading = false;
        }
    }

    public void Clear()
    {
        _loadRequest++;
        _workspaceId = null;
        ResetDisplay();
        ErrorMessage = null;
        ShowSkeleton = false;
        IsLoading = false;
    }

    internal static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }

    internal static string FormatCpu(double percent) => $"{percent:0.0}%";

    private void Apply(WorkspaceOverviewDto overview)
    {
        DisplayName = overview.DisplayName;
        State = overview.State;
        RepositoryPath = overview.RepositoryPath;
        WorktreePath = overview.WorktreePath;
        Commit = ShortCommit(overview.Commit);
        Cpu = FormatCpu(overview.CpuPercent);
        Memory = FormatBytes(overview.MemoryBytes);
        Storage = FormatBytes(overview.StorageBytes);
        ProcessCount = overview.ProcessCount.ToString();
        ApplicationCount = overview.ApplicationCount.ToString();
        ErrorMessage = null;
    }

    private void ResetDisplay()
    {
        DisplayName = string.Empty;
        State = string.Empty;
        RepositoryPath = string.Empty;
        WorktreePath = string.Empty;
        Commit = string.Empty;
        ClearResources();
    }

    private void ClearResources()
    {
        Cpu = "—";
        Memory = "—";
        Storage = "—";
        ProcessCount = "—";
        ApplicationCount = "—";
    }

    private static string ShortCommit(string commit)
        => commit.Length <= 8 ? commit : commit[..8];
}
