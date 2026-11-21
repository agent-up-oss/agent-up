using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;

namespace AgentUp.Server.Tests.Support;

internal sealed class StubAgentCapability : IAgentCapability
{
    public CapabilityIdentity Identity { get; init; } = new("codex", "1.0.0", "Codex", "agent-up");

    public string Kind => CapabilityKind.Agent;

    public IReadOnlyList<CapabilitySoftware> Software { get; init; } = [];

    public IReadOnlyList<CapabilitySettingSpec> Settings { get; init; } = [];

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; init; } = [];

    public bool SupportsPermissionRequests { get; init; } = true;

    public AgentLoginSpec? Login { get; init; }

    public AgentLaunchResult LaunchResult { get; init; } = new("codex-acp", [], null);

    /// <summary>The settings the last launch was given, so a test can see what the Server passed.</summary>
    public IReadOnlyDictionary<string, string>? LaunchedWith { get; private set; }

    public AgentLaunchResult Launch(IReadOnlyDictionary<string, string> settings)
    {
        LaunchedWith = settings;
        return LaunchResult;
    }
}
