using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Interfaces;

public interface IForgeCredentialStore
{
    string? Read(string workspaceId, string moduleId);

    void Write(string workspaceId, string moduleId, string token);

    void Delete(string workspaceId, string moduleId);

    /// <summary>The stored grant, with its refresh token and expiry where the store keeps them.</summary>
    ForgeOAuthGrant? ReadGrant(string workspaceId, string moduleId) =>
        Read(workspaceId, moduleId) is { } token ? new ForgeOAuthGrant(token, null, null) : null;

    void WriteGrant(string workspaceId, string moduleId, ForgeOAuthGrant grant) =>
        Write(workspaceId, moduleId, grant.AccessToken);
}
