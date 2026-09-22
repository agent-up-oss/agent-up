using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;

namespace AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

public sealed class CursorAgentCapability : IAgentCapability
{
    public const string PackageId = "cursor";
    public const string PackageVersion = "1.0.0";

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, "Cursor", "agent-up");

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [new("nodejs_22")];

    public bool CanRun => true;

    public AgentLoginSpec? Login { get; } = new("agent", ["login"], "poll");

    public AgentLaunchResult Launch() => new("agent", ["acp"], "nodejs_22");
}
