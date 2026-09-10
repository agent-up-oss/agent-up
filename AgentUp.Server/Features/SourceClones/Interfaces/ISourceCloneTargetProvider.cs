using AgentUp.Server.Features.SourceClones.DTOs;

namespace AgentUp.Server.Features.SourceClones.Interfaces;

public interface ISourceCloneTargetProvider
{
    SourceCloneTarget Resolve(CloneSourceRequest request);

    bool DestinationExists(SourceCloneTarget target);
}
