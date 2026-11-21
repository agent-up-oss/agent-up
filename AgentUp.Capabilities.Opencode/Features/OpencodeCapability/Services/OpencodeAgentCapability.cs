using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Providers;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;

namespace AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Services;

/// <summary>
/// OpenCode as an ACP coding agent, backed by a model served from an Ollama-compatible API.
/// OpenCode needs no subscription sign-in: what it needs is where the model is served and,
/// for a hosted endpoint, the token for it.
/// </summary>
public sealed class OpencodeAgentCapability(OpencodeConfigProvider config) : IAgentCapability
{
    public const string PackageId = "opencode";
    public const string PackageVersion = "1.0.0";
    public const string NixPackage = "opencode";

    public const string EndpointKey = "endpoint";
    public const string TokenKey = "token";
    public const string ModelKey = "model";

    public OpencodeAgentCapability()
        : this(new OpencodeConfigProvider())
    {
    }

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, "OpenCode", "agent-up");

    public string Kind => CapabilityKind.Agent;

    /// <summary>Nix delivers the pinned OpenCode release, which serves ACP itself.</summary>
    public IReadOnlyList<CapabilitySoftware> Software { get; } = [new("opencode", DeliveredByNix: true)];

    public IReadOnlyList<CapabilitySettingSpec> Settings { get; } =
    [
        new(EndpointKey, "Endpoint", CapabilitySettingKind.Url, Required: true, Placeholder: "http://localhost:11434"),
        new(TokenKey, "Token", CapabilitySettingKind.Secret),
        new(ModelKey, "Model", CapabilitySettingKind.Text, Required: true, Placeholder: "qwen2.5-coder:7b")
    ];

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [new(NixPackage)];

    public bool SupportsPermissionRequests => true;

    public AgentLoginSpec? Login => null;

    public AgentLaunchResult Launch(IReadOnlyDictionary<string, string> settings)
    {
        var endpoint = Value(settings, EndpointKey) ?? "";
        var model = Value(settings, ModelKey) ?? "";
        var token = Value(settings, TokenKey);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OpencodeConfigProvider.ConfigVariable] = config.Write(endpoint, model, token is not null)
        };
        if (token is not null)
            environment[OpencodeConfigProvider.TokenVariable] = token;
        return new AgentLaunchResult("opencode", ["acp"], NixPackage, environment);
    }

    private static string? Value(IReadOnlyDictionary<string, string> settings, string key)
        => settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
