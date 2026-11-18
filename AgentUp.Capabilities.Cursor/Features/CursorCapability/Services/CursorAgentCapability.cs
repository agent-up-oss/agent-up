using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;

namespace AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

public sealed class CursorAgentCapability : IAgentCapability
{
    public const string PackageId = "cursor";
    public const string PackageVersion = "1.0.0";

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, "Cursor", "agent-up");

    public string Kind => CapabilityKind.Agent;

    /// <summary>Nix delivers Node.js; the ACP adapter itself has to be on the Server's search path.</summary>
    public IReadOnlyList<CapabilitySoftware> Software { get; } =
        [new("node", DeliveredByNix: true), new("agent", DeliveredByNix: false)];

    public IReadOnlyList<CapabilitySettingSpec> Settings { get; } = [];

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [new("nodejs_22")];

    public bool SupportsPermissionRequests => true;

    public AgentLoginSpec? Login { get; } = new("agent", ["login"], "poll");

    public AgentLaunchResult Launch(IReadOnlyDictionary<string, string> settings) => new("agent", ["acp"], "nodejs_22");
}
