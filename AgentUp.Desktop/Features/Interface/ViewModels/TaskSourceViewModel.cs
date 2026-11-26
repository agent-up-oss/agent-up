using System.Reactive;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// Each team member's GitHub or Forgejo connection. A host URL and personal access token are
/// sent to the Server and the token is not kept on this view: the Server checks that it can list
/// repositories and read pull requests.
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

    public IReadOnlyList<TaskSourceIdentity> Identities => _state?.Identities ?? [];

    public IReadOnlyList<TaskSourceRepository> Repositories => _repositories;

    public string? Error => _error;

    public bool HasError => _error is { Length: > 0 };

    public TaskSourceIdentity? IdentityFor(string? workspaceId)
        => Identities.FirstOrDefault(identity => identity.WorkspaceId.Equals(workspaceId, StringComparison.OrdinalIgnoreCase));

    public string DetailFor(string? workspaceId, string moduleId)
    {
        if (_error is { Length: > 0 })
            return _error;
        var identity = IdentityFor(workspaceId);
        if (identity is null)
            return "Add a workspace to give a team member their own access token.";
        if (identity.ModuleId is not null
            && identity.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase))
        {
            return identity.Detail;
        }

        return $"Paste the host URL and a personal access token for {identity.DisplayName}. It needs access to repositories and pull requests.";
    }

    public bool IsConnected(string? workspaceId, string moduleId)
    {
        var identity = IdentityFor(workspaceId);
        return identity is { Connected: true }
            && identity.ModuleId is not null
            && identity.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The host URL the field should show: the saved origin when this member is on this host, or
    /// github.com when this tab is GitHub and nothing is saved yet.
    /// </summary>
    public string EndpointFor(string? workspaceId, string moduleId)
    {
        var identity = IdentityFor(workspaceId);
        var saved = identity is not null
            && identity.ModuleId is not null
            && identity.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase)
            ? identity.EndpointUrl
            : null;
        return CommandCenterScene.PrefillEndpoint(moduleId, saved);
    }

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

    public async Task SaveAsync(
        string workspaceId,
        string moduleId,
        string endpointUrl,
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _state = await _source.SaveAsync(workspaceId, moduleId, endpointUrl, token, cancellationToken);
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

    public async Task ForgetAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        try
        {
            _state = await _source.ForgetAsync(workspaceId, cancellationToken);
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
        this.RaisePropertyChanged(nameof(Identities));
        this.RaisePropertyChanged(nameof(Error));
        this.RaisePropertyChanged(nameof(HasError));
    }
}
