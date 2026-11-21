using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <param name="sandbox">Confines every ACP agent to its team member's permission set.</param>
/// <param name="settings">Where a workspace's team member kind is read from.</param>
public sealed class AgentProcessFactory(
    AgentCommandProvider commands,
    ILoggerFactory loggerFactory,
    IInProcessChatModel? model = null,
    IInProcessToolHost? tools = null,
    IAgentSandbox? sandbox = null,
    IAgentWorkspaceSettingsRepository? settings = null) : IAgentProcessFactory
{
    public IAgentProcessProvider Create() =>
        new AcpProcessProvider(commands, loggerFactory.CreateLogger<AcpProcessProvider>(), sandbox);

    public IAgentProcessProvider Create(string agent, string workspaceId)
    {
        if (!InProcessAgents.IsInProcess(agent))
        {
            var member = settings?.Get(workspaceId).Member ?? TeamMemberKind.Author;
            return new AcpProcessProvider(
                commands,
                loggerFactory.CreateLogger<AcpProcessProvider>(),
                sandbox,
                TeamMemberGrants.For(member));
        }
        if (model is null || tools is null)
            throw new InvalidOperationException($"{agent} is an in-process agent and this Server has no in-process agent host.");
        return new InProcessAgentProcessProvider(
            workspaceId,
            model,
            tools,
            loggerFactory.CreateLogger<InProcessAgentProcessProvider>());
    }
}
