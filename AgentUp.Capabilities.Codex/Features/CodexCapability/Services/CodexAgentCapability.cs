using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;

namespace AgentUp.Capabilities.Codex.Features.CodexCapability.Services;

public sealed class CodexAgentCapability : IAgentCapability
{
    public const string PackageId = "codex";
    public const string PackageVersion = "1.0.1";

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, "Codex", "agent-up");

    public string Kind => CapabilityKind.Agent;

    /// <summary>Nix delivers Node.js; the ACP adapter itself has to be on the Server's search path.</summary>
    public IReadOnlyList<CapabilitySoftware> Software { get; } =
        [new("node", DeliveredByNix: true), new("codex-acp", DeliveredByNix: false)];

    public IReadOnlyList<CapabilitySettingSpec> Settings { get; } = [];

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [new("nodejs_22")];

    public bool SupportsPermissionRequests => true;

    public AgentLoginSpec? Login { get; } = new("codex", ["login", "--device-auth"], "device");

    public AgentLaunchResult Launch(IReadOnlyDictionary<string, string> settings) => new("codex-acp", [], "nodejs_22");
}
