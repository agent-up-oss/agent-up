using AgentUp.Desktop.Features.Authentication.DTOs;
using AgentUp.Desktop.Features.Authentication.Interfaces;
using AgentUp.Desktop.Features.Authentication.Models;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.FakeServer.Controllers;
using AgentUp.Desktop.Features.FakeServer.Models;

namespace AgentUp.Desktop.Features.Authentication.Services;

public sealed class ServerConnectionService(
    IServerConnectionStore store,
    HttpClient http,
    FakeServerController fakeServers)
{
    public SavedServerListDto List()
    {
        var selection = store.Load();
        return ToListDto(selection);
    }

    public SavedServerDto Save(string url, string? accessToken)
    {
        if (fakeServers.Matches(url))
            return ActivateFake();

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
        if (string.Equals(id, FakeServerIdentity.Id, StringComparison.Ordinal))
            return ActivateFake();

        var selection = store.Load();
        var server = selection.Servers.FirstOrDefault(candidate => candidate.Id == id)
            ?? throw new InvalidOperationException("That saved server is no longer available.");
        if (FakeServerIdentity.Matches(server.Url))
            return ActivateFake();

        selection.ActiveServerId = server.Id;
        store.Save(selection);
        var uri = SecureServerUrlProvider.ResolveServerUri(server.Url);
        ServerSessionProvider.Apply(http, uri, server.AccessToken);
        return ToDto(server, server.Id);
    }

    public void Remove(string id)
    {
        if (string.Equals(id, FakeServerIdentity.Id, StringComparison.Ordinal))
            return;

        var selection = store.Load();
        selection.Servers.RemoveAll(server =>
            server.Id == id && !FakeServerIdentity.Matches(server.Url));
        if (selection.ActiveServerId == id)
            selection.ActiveServerId = selection.Servers.FirstOrDefault()?.Id;
        store.Save(selection);
    }

    public void Prepare(string url)
    {
        if (fakeServers.Matches(url))
        {
            fakeServers.Reset();
            ServerSessionProvider.Apply(
                http,
                SecureServerUrlProvider.ResolveServerUri(FakeServerIdentity.Url),
                null);
            return;
        }

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
        if (string.Equals(selection.ActiveServerId, FakeServerIdentity.Id, StringComparison.Ordinal)
            || selection.Servers.Any(server =>
                server.Id == selection.ActiveServerId && FakeServerIdentity.Matches(server.Url)))
        {
            ActivateFake();
            return;
        }

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

    private SavedServerDto ActivateFake()
    {
        fakeServers.Reset();
        var selection = store.Load();
        var existing = selection.Servers.FirstOrDefault(server =>
            FakeServerIdentity.Matches(server.Url) || server.Id == FakeServerIdentity.Id);
        if (existing is null)
        {
            existing = new ConfiguredServer
            {
                Id = FakeServerIdentity.Id,
                Url = FakeServerIdentity.Url
            };
            selection.Servers.Insert(0, existing);
        }

        existing.Id = FakeServerIdentity.Id;
        existing.Url = FakeServerIdentity.Url;
        selection.ActiveServerId = FakeServerIdentity.Id;
        store.Save(selection);
        ServerSessionProvider.Apply(
            http,
            SecureServerUrlProvider.ResolveServerUri(FakeServerIdentity.Url),
            null);
        return ToFakeDto(true);
    }

    private SavedServerListDto ToListDto(ServerSelection selection)
    {
        var fakeActive = string.Equals(selection.ActiveServerId, FakeServerIdentity.Id, StringComparison.Ordinal)
            || selection.Servers.Any(server =>
                server.Id == selection.ActiveServerId && FakeServerIdentity.Matches(server.Url));
        var servers = new List<SavedServerDto> { ToFakeDto(fakeActive) };
        servers.AddRange(selection.Servers
            .Where(server => !FakeServerIdentity.Matches(server.Url) && server.Id != FakeServerIdentity.Id)
            .Select(server => ToDto(server, selection.ActiveServerId)));

        var current = servers.FirstOrDefault(server => server.IsActive)?.Url
            ?? servers.FirstOrDefault(server => !server.IsFake)?.Url
            ?? "";
        return new SavedServerListDto(servers, current);
    }

    private static SavedServerDto ToDto(ConfiguredServer server, string? activeServerId)
        => new(
            server.Id,
            server.Url,
            !string.IsNullOrWhiteSpace(server.AccessToken),
            server.Id == activeServerId,
            server.Url,
            true,
            false);

    private static SavedServerDto ToFakeDto(bool isActive)
        => new(
            FakeServerIdentity.Id,
            FakeServerIdentity.Url,
            false,
            isActive,
            FakeServerIdentity.DisplayName,
            false,
            true);
}
