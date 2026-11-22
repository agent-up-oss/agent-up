using AgentUp.Server.Features.Files.Interfaces;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeWorkspaceFileEditGuard : IWorkspaceFileEditGuard
{
    public bool Working { get; set; }

    public bool IsAgentWorking(string workspaceId) => Working;
}
