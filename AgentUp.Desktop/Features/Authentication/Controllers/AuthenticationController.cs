using AgentUp.Desktop.Features.Authentication.DTOs;
using AgentUp.Desktop.Features.Authentication.Models;
using AgentUp.Desktop.Features.Authentication.Services;

namespace AgentUp.Desktop.Features.Authentication.Controllers;

public sealed class AuthenticationController(AuthenticationService service, ServerConnectionService connections)
{
    public Task<bool> IsRequiredAsync(CancellationToken cancellationToken = default)
        => service.IsRequiredAsync(cancellationToken);

    public Task<ConnectionSource> ResolveConnectionAsync(CancellationToken cancellationToken = default)
        => service.ResolveConnectionAsync(cancellationToken);

    public Task<string> LoginAsync(string password, CancellationToken cancellationToken = default)
        => service.LoginAsync(password, cancellationToken);

    public SavedServerListDto ListSavedServers() => connections.List();

    public SavedServerDto SaveServer(string url, string? accessToken) => connections.Save(url, accessToken);

    public SavedServerDto ActivateServer(string id) => connections.Activate(id);

    public void RemoveServer(string id) => connections.Remove(id);

    public void PrepareServer(string url) => connections.Prepare(url);

    public string CurrentServerUrl() => connections.CurrentUrl();

    public string CurrentConnectionId() => connections.CurrentId();

    public ClientSurfaceAvailability ClientSurfaces() => connections.Surfaces();
}
