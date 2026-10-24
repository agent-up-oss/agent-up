using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;

namespace AgentUp.Server.Features.Forge.Services;

public sealed class GithubAppConnectionService(
    IGithubAppConnectionStore connections,
    GithubAppApiProvider api)
{
    public bool IsConnected(string workspaceId)
        => connections.Read(workspaceId) is { InstallationId: not null, PrivateKey: not null };

    public async Task<ForgeLoginStartDto> StartAsync(
        string workspaceId,
        string workspaceName,
        string repositoryOwner,
        ForgeHostConfiguration host,
        string callbackOrigin,
        CancellationToken cancellationToken)
    {
        var existing = connections.Read(workspaceId);
        if (existing is { Slug: not null })
            return new ForgeLoginStartDto($"{existing.WebOrigin}/apps/{Uri.EscapeDataString(existing.Slug)}/installations/new");

        var webOrigin = new Uri(host.OAuthAuthorizeUrl).GetLeftPart(UriPartial.Authority);
        var organizationOwned = await api.IsOrganizationAsync(host.ApiBaseUrl, repositoryOwner, cancellationToken);
        var pending = new GithubAppConnection(
            workspaceId,
            Token(),
            Token(),
            AppName(workspaceName, workspaceId),
            callbackOrigin.TrimEnd('/'),
            host.ApiBaseUrl,
            webOrigin,
            RegistrationOwner: organizationOwned ? repositoryOwner : null,
            RepositoryOwner: repositoryOwner);
        connections.Write(pending);
        return new ForgeLoginStartDto(
            $"{pending.CallbackOrigin}/api/forge/github/register?state={Uri.EscapeDataString(pending.State)}");
    }

    public string RegistrationPage(string state)
    {
        var pending = connections.FindByState(state)
                      ?? throw new InvalidOperationException("The GitHub App registration request is invalid or expired.");
        var manifest = JsonSerializer.Serialize(new
        {
            name = pending.AppName,
            url = pending.CallbackOrigin,
            redirect_url = $"{pending.CallbackOrigin}/api/forge/github/manifest/callback",
            setup_url = $"{pending.CallbackOrigin}/api/forge/github/install/callback?key={Uri.EscapeDataString(pending.SetupKey)}",
            setup_on_update = true,
            @public = false,
            default_permissions = new
            {
                metadata = "read",
                contents = "write",
                issues = "write",
                pull_requests = "write",
                checks = "read"
            },
            default_events = Array.Empty<string>()
        });
        var encoded = System.Net.WebUtility.HtmlEncode(manifest);
        var ownerPath = string.IsNullOrWhiteSpace(pending.RegistrationOwner)
            ? string.Empty
            : $"/organizations/{Uri.EscapeDataString(pending.RegistrationOwner)}";
        var action = System.Net.WebUtility.HtmlEncode(
            $"{pending.WebOrigin}{ownerPath}/settings/apps/new?state={Uri.EscapeDataString(state)}");
        return $"""
            <!doctype html><html><head><meta charset="utf-8"><title>Create workspace GitHub App</title></head>
            <body><form id="manifest" method="post" action="{action}">
            <input type="hidden" name="manifest" value="{encoded}"></form>
            <p>Continuing to GitHub…</p><script>document.getElementById('manifest').submit();</script></body></html>
            """;
    }

    public async Task<string?> CompleteManifestAsync(
        string code, string state, CancellationToken cancellationToken)
    {
        var pending = connections.FindByState(state);
        if (pending is null) return null;
        var registered = (await api.ConvertManifestAsync(pending, code, cancellationToken)) with { State = string.Empty };
        connections.Write(registered);
        return string.IsNullOrWhiteSpace(registered.Slug)
            ? null
            : $"{registered.WebOrigin}/apps/{Uri.EscapeDataString(registered.Slug)}/installations/new";
    }

    public async Task<bool> CompleteInstallationAsync(
        string setupKey, long installationId, CancellationToken cancellationToken)
    {
        var connection = connections.FindBySetupKey(setupKey);
        if (connection is null || !await api.VerifyInstallationAsync(connection, installationId, cancellationToken))
            return false;
        connections.Write(connection with { InstallationId = installationId });
        return true;
    }

    public async Task<string?> TokenAsync(
        string workspaceId, string repository, CancellationToken cancellationToken)
    {
        var connection = connections.Read(workspaceId);
        return connection is { InstallationId: not null, PrivateKey: not null }
            ? await api.CreateInstallationTokenAsync(connection, repository, cancellationToken)
            : null;
    }

    public void Disconnect(string workspaceId)
    {
        var connection = connections.Read(workspaceId);
        if (connection is not null)
            connections.Write(connection with { InstallationId = null });
    }

    private static string Token()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static string AppName(string workspaceName, string workspaceId)
    {
        var safe = Regex.Replace(workspaceName, "[^A-Za-z0-9-]+", "-").Trim('-');
        if (safe.Length > 16) safe = safe[..16].TrimEnd('-');
        if (string.IsNullOrEmpty(safe)) safe = "Workspace";
        var suffix = Regex.Replace(workspaceId, "[^A-Za-z0-9]", "");
        suffix = suffix.Length >= 8
            ? suffix[..8]
            : Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(workspaceId)))[..8].ToLowerInvariant();
        return $"Agent-Up-{safe}-{suffix}";
    }
}
