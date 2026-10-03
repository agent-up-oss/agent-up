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
    FakeServerController fakeServers,
    HttpClient? agentEventsHttp = null,
    RecommendedServer? recommended = null)
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
                Id = MatchesRecommended(normalized) ? RecommendedId() : Guid.NewGuid().ToString("N"),
                Url = normalized,
                DisplayName = RecommendedDisplayName(normalized)
            };
            selection.Servers.Add(existing);
        }

        existing.Url = normalized;
        existing.DisplayName = RecommendedDisplayName(normalized) ?? existing.DisplayName;
        if (accessToken is not null)
            existing.AccessToken = accessToken;

        selection.ActiveServerId = existing.Id;
        store.Save(selection);
        ApplySession(uri, existing.AccessToken);
        return ToDto(existing, existing.Id);
    }

    public SavedServerDto Activate(string id)
    {
        if (string.Equals(id, FakeServerIdentity.Id, StringComparison.Ordinal))
            return ActivateFake();

        if (recommended is not null
            && (string.Equals(id, recommended.Id, StringComparison.Ordinal)
                || string.Equals(id, RecommendedServer.RecommendedId, StringComparison.Ordinal)))
            return Save(recommended.Url, accessToken: null);

        var selection = store.Load();
        var server = selection.Servers.FirstOrDefault(candidate => candidate.Id == id)
            ?? throw new InvalidOperationException("That saved server is no longer available.");
        if (FakeServerIdentity.Matches(server.Url))
            return ActivateFake();

        selection.ActiveServerId = server.Id;
        store.Save(selection);
        var uri = SecureServerUrlProvider.ResolveServerUri(server.Url);
        ApplySession(uri, server.AccessToken);
        return ToDto(server, server.Id);
    }

    public void Remove(string id)
    {
        if (string.Equals(id, FakeServerIdentity.Id, StringComparison.Ordinal))
            return;
        if (recommended is not null
            && (string.Equals(id, recommended.Id, StringComparison.Ordinal)
                || string.Equals(id, RecommendedServer.RecommendedId, StringComparison.Ordinal)))
            return;

        var selection = store.Load();
        var target = selection.Servers.FirstOrDefault(server => server.Id == id);
        if (target is not null && MatchesRecommended(target.Url))
            return;

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
            ApplySession(SecureServerUrlProvider.ResolveServerUri(FakeServerIdentity.Url), null);
            return;
        }

        var uri = SecureServerUrlProvider.ResolveServerUri(url);
        var selection = store.Load();
        var normalized = SecureServerUrlProvider.Normalize(uri);
        var existing = selection.Servers.FirstOrDefault(server =>
            string.Equals(server.Url, normalized, StringComparison.OrdinalIgnoreCase));
        ApplySession(uri, existing?.AccessToken);
    }

    public ClientSurfaceAvailability Surfaces()
        => ClientSurfaceAvailability.ForActiveServer(FakeServerIdentity.Matches(CurrentUrl()));

    public string CurrentUrl()
    {
        var uri = ServerSessionProvider.CurrentUri(http)
            ?? SecureServerUrlProvider.ResolveServerUri();
        return SecureServerUrlProvider.Normalize(uri);
    }

    public string CurrentId()
    {
        var url = CurrentUrl();
        var match = List().Servers.FirstOrDefault(server =>
            string.Equals(server.Url, url, StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? url;
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
        ApplySession(SecureServerUrlProvider.ResolveServerUri(FakeServerIdentity.Url), null);
        return ToFakeDto(true);
    }

    private void ApplySession(Uri uri, string? accessToken)
    {
        ServerSessionProvider.Apply(http, uri, accessToken);
        if (agentEventsHttp is not null)
            ServerSessionProvider.Apply(agentEventsHttp, uri, accessToken);
    }

    private SavedServerListDto ToListDto(ServerSelection selection)
    {
        var fakeActive = string.Equals(selection.ActiveServerId, FakeServerIdentity.Id, StringComparison.Ordinal)
            || selection.Servers.Any(server =>
                server.Id == selection.ActiveServerId && FakeServerIdentity.Matches(server.Url));
        var servers = new List<SavedServerDto> { ToFakeDto(fakeActive) };
        if (recommended is not null)
            servers.Add(ToRecommendedDto(selection));
        servers.AddRange(selection.Servers
            .Where(server => !FakeServerIdentity.Matches(server.Url)
                && server.Id != FakeServerIdentity.Id
                && !MatchesRecommended(server.Url))
            .Select(server => ToDto(server, selection.ActiveServerId)));

        var current = servers.FirstOrDefault(server => server.IsActive)?.Url
            ?? servers.FirstOrDefault(server => !server.IsFake && !server.IsRecommended)?.Url
            ?? "";
        return new SavedServerListDto(servers, current);
    }

    private SavedServerDto ToRecommendedDto(ServerSelection selection)
    {
        var saved = selection.Servers.FirstOrDefault(server => MatchesRecommended(server.Url));
        var id = saved?.Id ?? recommended!.Id;
        return new SavedServerDto(
            id,
            recommended!.Url,
            !string.IsNullOrWhiteSpace(saved?.AccessToken),
            id == selection.ActiveServerId,
            recommended.DisplayName,
            false,
            false,
            true);
    }

    private SavedServerDto ToDto(ConfiguredServer server, string? activeServerId)
        => new(
            server.Id,
            server.Url,
            !string.IsNullOrWhiteSpace(server.AccessToken),
            server.Id == activeServerId,
            RecommendedDisplayName(server.Url) ?? server.DisplayName ?? server.Url,
            !MatchesRecommended(server.Url),
            false,
            MatchesRecommended(server.Url));

    private static SavedServerDto ToFakeDto(bool isActive)
        => new(
            FakeServerIdentity.Id,
            FakeServerIdentity.Url,
            false,
            isActive,
            FakeServerIdentity.DisplayName,
            false,
            true,
            false);

    private bool MatchesRecommended(string url)
        => recommended is not null
           && string.Equals(url, recommended.Url, StringComparison.OrdinalIgnoreCase);

    private string RecommendedId() => recommended?.Id ?? RecommendedServer.RecommendedId;

    private string? RecommendedDisplayName(string url)
        => MatchesRecommended(url) ? recommended!.DisplayName : null;
}
