using AgentUp.Server.Features.SourceClones.DTOs;

namespace AgentUp.Server.Features.SourceClones.Interfaces;

public interface ISourceCloneGitProvider
{
    Task CloneAsync(SourceCloneTarget target, CancellationToken cancellationToken = default);

    Task RefreshAsync(string destinationPath, string revision, CancellationToken cancellationToken = default);
}
