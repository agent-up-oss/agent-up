using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Tests.Support;

internal sealed class MemoryForgeCredentialStore : IForgeCredentialStore
{
    private readonly Dictionary<string, string> _tokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ForgeOAuthGrant> _grants = new(StringComparer.Ordinal);

    public ForgeOAuthGrant? ReadGrant(string workspaceId, string moduleId)
        => _grants.GetValueOrDefault(Key(workspaceId, moduleId))
           ?? (Read(workspaceId, moduleId) is { } token ? new ForgeOAuthGrant(token, null, null) : null);

    public void WriteGrant(string workspaceId, string moduleId, ForgeOAuthGrant grant)
    {
        _grants[Key(workspaceId, moduleId)] = grant;
        _tokens[Key(workspaceId, moduleId)] = grant.AccessToken;
    }

    public string? Read(string workspaceId, string moduleId)
        => _tokens.GetValueOrDefault(Key(workspaceId, moduleId));

    public void Write(string workspaceId, string moduleId, string token)
        => _tokens[Key(workspaceId, moduleId)] = token;

    public void Delete(string workspaceId, string moduleId)
        => _tokens.Remove(Key(workspaceId, moduleId));

    private static string Key(string workspaceId, string moduleId) => workspaceId + "/" + moduleId;
}

internal sealed class FixedForgeGitRemoteResolver(ForgeGitRemote? remote) : IForgeGitRemoteResolver
{
    public ForgeGitRemote? Resolve(string worktreePath) => remote;
}

internal sealed class FixedForgeOAuthSettings(ForgeOAuthConfiguration configuration) : IForgeOAuthSettings
{
    public ForgeOAuthConfiguration Read() => configuration;
}

internal sealed class FixedForgeOAuthExchange(string token, string? refusal = null, ForgeOAuthGrant? refreshed = null) : IForgeOAuthExchange
{
    public string? LastTokenUrl { get; private set; }

    public List<string> RefreshedWith { get; } = [];

    public Task<ForgeOAuthGrant> RefreshAsync(string refreshToken, string tokenUrl, CancellationToken cancellationToken)
    {
        RefreshedWith.Add(refreshToken);
        return refreshed is null
            ? Task.FromException<ForgeOAuthGrant>(new InvalidOperationException("The host refused the refresh token."))
            : Task.FromResult(refreshed);
    }

    public Task<string> ExchangeAsync(string code, string tokenUrl, CancellationToken cancellationToken)
    {
        LastTokenUrl = tokenUrl;
        return refusal is null ? Task.FromResult(token) : Task.FromException<string>(new InvalidOperationException(refusal));
    }
}
