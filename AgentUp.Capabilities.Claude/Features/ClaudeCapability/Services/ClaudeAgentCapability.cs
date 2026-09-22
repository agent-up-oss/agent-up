using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;

namespace AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;

public sealed class ClaudeAgentCapability : IAgentCapability
{
    public const string PackageId = "claude";
    public const string PackageVersion = "1.0.0";

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, "Claude", "agent-up");

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [new("nodejs_22")];

    public bool CanRun => true;

    public AgentLoginSpec? Login { get; } = new("claude", ["setup-token"], "code");

    public AgentLaunchResult Launch() => new("claude-agent-acp", [], "nodejs_22");
}
