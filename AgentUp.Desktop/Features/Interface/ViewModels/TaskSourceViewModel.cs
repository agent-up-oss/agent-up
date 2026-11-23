using System.Reactive;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Models;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// The GitHub and Forgejo connections. A personal access token is sent to the Server and is not
/// kept on this view: the Server checks that it can list repositories and read pull requests.
/// </summary>
public sealed class TaskSourceViewModel : ReactiveObject
{
    private readonly TaskSourceController _source;
    private TaskSource? _state;
    private string? _error;
    private IReadOnlyList<TaskSourceRepository> _repositories = [];

    public TaskSourceViewModel(TaskSourceController source)
    {
        _source = source;
        RefreshCommand = ReactiveCommand.CreateFromTask(() => LoadAsync());
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public IReadOnlyList<TaskSourceModule> Modules => _state?.Modules ?? [];

    public IReadOnlyList<TaskSourceRepository> Repositories => _repositories;

    public string? Error => _error;

    public bool HasError => _error is { Length: > 0 };

    public string DetailFor(string moduleId)
    {
        if (_error is { Length: > 0 })
            return _error;
        return Modules.FirstOrDefault(module => module.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase))?.Detail
            ?? "Paste a personal access token. It needs access to repositories and pull requests.";
    }

    public bool IsConnected(string moduleId) =>
        Modules.Any(module => module.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase) && module.Connected);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _state = await _source.ReadAsync(cancellationToken);
            _error = null;
            Raise();
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            _error = failure.Message;
            Raise();
        }
    }

    public async Task SaveAsync(string moduleId, string token, CancellationToken cancellationToken = default)
    {
        try
        {
            _state = await _source.SaveAsync(moduleId, token, cancellationToken);
            _error = null;
            Raise();
            await LoadRepositoriesAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            _error = failure.Message;
            Raise();
        }
    }

    public async Task ForgetAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        try
        {
            _state = await _source.ForgetAsync(moduleId, cancellationToken);
            _error = null;
            Raise();
            await LoadRepositoriesAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            _error = failure.Message;
            Raise();
        }
    }

    public async Task LoadRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _repositories = await _source.ListRepositoriesAsync(cancellationToken);
            this.RaisePropertyChanged(nameof(Repositories));
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            _error = failure.Message;
            Raise();
        }
    }

    public Task<IReadOnlyList<TaskSourcePull>> PullsAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
        => _source.ListPullsAsync(moduleId, owner, repository, cancellationToken);

    private void Raise()
    {
        this.RaisePropertyChanged(nameof(Modules));
        this.RaisePropertyChanged(nameof(Error));
        this.RaisePropertyChanged(nameof(HasError));
    }
}
