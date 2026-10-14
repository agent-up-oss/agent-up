using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Interfaces;

public interface IForgeGitRemoteResolver
{
    ForgeGitRemote? Resolve(string worktreePath);
}
