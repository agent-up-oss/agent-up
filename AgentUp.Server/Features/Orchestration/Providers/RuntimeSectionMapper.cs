using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Orchestration.Providers;

public static class RuntimeSectionMapper
{
    public static IReadOnlyList<RuntimeSectionDefinition> Effective(AgentUpConfiguration config)
        => RuntimeSectionDefinition.Merge(config.RuntimeSections, config.Dotnet, config.Docker);

    public static IReadOnlyList<RuntimeSectionDefinition> Effective(RegisterWorkspaceRequest request)
        => RuntimeSectionDefinition.Merge(request.RuntimeSections, request.Dotnet, request.Docker);
}
