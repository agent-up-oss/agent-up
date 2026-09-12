using AgentUp.Server.Features.Git.Interfaces;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeWorkspacePromptGuard : IWorkspacePromptGuard
{
    public bool Running { get; set; }

    public bool IsPromptRunning(string workspaceId) => Running;
}
