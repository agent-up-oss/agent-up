using AgentUp.Desktop.Features.Authentication.DTOs;
using AgentUp.Desktop.Features.Authentication.Interfaces;
using AgentUp.Desktop.Features.Authentication.Models;
using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Features.Authentication.Services;

public sealed class ServerConnectionService(IServerConnectionStore store, HttpClient http)
{
    public SavedServerListDto List()
    {
        var selection = store.Load();
        return ToListDto(selection);
    }

    public SavedServerDto Save(string url, string? accessToken)
    {
        var uri = SecureServerUrlProvider.ResolveServerUri(url);
        var normalized = SecureServerUrlProvider.Normalize(uri);
        var selection = store.Load();
        var existing = selection.Servers.FirstOrDefault(server =>
            string.Equals(server.Url, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new ConfiguredServer
            {
                Id = Guid.NewGuid().ToString("N"),
                Url = normalized
            };
            selection.Servers.Add(existing);
        }

        existing.Url = normalized;
        if (accessToken is not null)
            existing.AccessToken = accessToken;

        selection.ActiveServerId = existing.Id;
        store.Save(selection);
        ServerSessionProvider.Apply(http, uri, existing.AccessToken);
        return ToDto(existing, existing.Id);
    }

    public SavedServerDto Activate(string id)
    {
        var selection = store.Load();
        var server = selection.Servers.FirstOrDefault(candidate => candidate.Id == id)
            ?? throw new InvalidOperationException("That saved server is no longer available.");
        selection.ActiveServerId = server.Id;
        store.Save(selection);
        var uri = SecureServerUrlProvider.ResolveServerUri(server.Url);
        ServerSessionProvider.Apply(http, uri, server.AccessToken);
        return ToDto(server, server.Id);
    }

    public void Remove(string id)
    {
        var selection = store.Load();
        selection.Servers.RemoveAll(server => server.Id == id);
        if (selection.ActiveServerId == id)
            selection.ActiveServerId = selection.Servers.FirstOrDefault()?.Id;
        store.Save(selection);
    }

    public void Prepare(string url)
    {
        var uri = SecureServerUrlProvider.ResolveServerUri(url);
        var selection = store.Load();
        var normalized = SecureServerUrlProvider.Normalize(uri);
        var existing = selection.Servers.FirstOrDefault(server =>
            string.Equals(server.Url, normalized, StringComparison.OrdinalIgnoreCase));
        ServerSessionProvider.Apply(http, uri, existing?.AccessToken);
    }

    public void RestoreActive()
    {
        var selection = store.Load();
        var active = selection.Servers.FirstOrDefault(server => server.Id == selection.ActiveServerId)
            ?? selection.Servers.FirstOrDefault();
        if (active is null)
            return;

        var uri = SecureServerUrlProvider.ResolveServerUri(active.Url);
        ServerSessionProvider.Apply(http, uri, active.AccessToken);
    }

    public string CurrentUrl()
    {
        var uri = ServerSessionProvider.CurrentUri(http)
            ?? SecureServerUrlProvider.ResolveServerUri();
        return SecureServerUrlProvider.Normalize(uri);
    }

    private static SavedServerListDto ToListDto(ServerSelection selection)
    {
        var servers = selection.Servers
            .Select(server => ToDto(server, selection.ActiveServerId))
            .ToList();
        var current = servers.FirstOrDefault(server => server.IsActive)?.Url
            ?? servers.FirstOrDefault()?.Url
            ?? "";
        return new SavedServerListDto(servers, current);
    }

    private static SavedServerDto ToDto(ConfiguredServer server, string? activeServerId)
        => new(server.Id, server.Url, !string.IsNullOrWhiteSpace(server.AccessToken), server.Id == activeServerId);
}
