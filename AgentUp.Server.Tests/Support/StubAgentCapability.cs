using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;

namespace AgentUp.Server.Tests.Support;

internal sealed class StubAgentCapability : IAgentCapability
{
    public CapabilityIdentity Identity { get; init; } = new("codex", "1.0.0", "Codex", "agent-up");

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [];

    public bool CanRun { get; init; } = true;

    public AgentLoginSpec? Login { get; init; }

    public AgentLaunchResult Launch() => new("codex-acp", [], null);
}
