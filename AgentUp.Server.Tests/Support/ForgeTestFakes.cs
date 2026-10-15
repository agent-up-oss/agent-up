using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Tests.Support;

internal sealed class MemoryForgeCredentialStore : IForgeCredentialStore
{
    private readonly Dictionary<string, string> _tokens = new(StringComparer.Ordinal);

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

internal sealed class FixedForgeOAuthExchange(string token) : IForgeOAuthExchange
{
    public Task<string> ExchangeAsync(string code, CancellationToken cancellationToken)
        => Task.FromResult(token);
}
