using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;

namespace AgentUp.Capabilities.Codex.Features.CodexCapability.Services;

public sealed class CodexAgentCapability : IAgentCapability
{
    public const string PackageId = "codex";
    public const string PackageVersion = "1.0.0";

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, "Codex", "agent-up");

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [new("nodejs_22")];

    public bool CanRun => true;

    public AgentLoginSpec? Login { get; } = new("codex", ["login", "--device-auth"], "code");

    public AgentLaunchResult Launch() => new("codex-acp", [], "nodejs_22");
}
