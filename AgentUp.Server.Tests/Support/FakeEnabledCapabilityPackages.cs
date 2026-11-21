using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;
using AgentUp.Sdk.Forge;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// Enabled capabilities for slices that consume them. Every module passed in is enabled and
/// ready unless <see cref="WithReadiness"/> says otherwise; <see cref="WithBuiltIn"/> adds a
/// module the Server has but has not enabled.
/// </summary>
internal sealed class FakeEnabledCapabilityPackages : IEnabledCapabilityPackages
{
    private readonly List<ICapability> _enabled = [];
    private readonly List<ICapability> _builtIn = [];
    private readonly Dictionary<string, CapabilityReadinessDto> _readiness = new(StringComparer.OrdinalIgnoreCase);

    public FakeEnabledCapabilityPackages(params ICapability[] modules)
    {
        _enabled.AddRange(modules);
    }

    public FakeEnabledCapabilityPackages WithAgents(params IAgentCapability[] agents) => Replace(agents);

    public FakeEnabledCapabilityPackages WithForges(params IForgeCapability[] forges) => Replace(forges);

    public FakeEnabledCapabilityPackages WithRuntimes(params IRuntimeCapability[] runtimes) => Replace(runtimes);

    public FakeEnabledCapabilityPackages WithBuiltIn(params ICapability[] modules)
    {
        _builtIn.AddRange(modules);
        return this;
    }

    public FakeEnabledCapabilityPackages WithReadiness(CapabilityReadinessDto readiness)
    {
        _readiness[readiness.Id] = readiness;
        return this;
    }

    public static FakeEnabledCapabilityPackages FirstParty()
        => new(Dotnet(), Docker(), Codex(), Cursor(), Claude());

    public CapabilityLaunchPlan? AgentLaunch(string id)
    {
        var agent = GetAgent(id);
        if (agent is null)
            return null;
        var launched = agent.Launch(new Dictionary<string, string>());
        return new CapabilityLaunchPlan(launched.FileName, Environment: launched.Environment, Arguments: launched.Arguments);
    }

    public CapabilityLaunchWrapDto WrapLaunch(string fileName, IReadOnlyList<string> arguments)
        => new(fileName, arguments);

    public CapabilityLaunchWrapDto WrapModule(string id, string fileName, IReadOnlyList<string> arguments)
        => new(fileName, arguments);

    public IRuntimeCapability? GetRuntime(string id) => Find<IRuntimeCapability>(id);

    public IReadOnlyList<IRuntimeCapability> ListRuntimes() => _enabled.OfType<IRuntimeCapability>().ToArray();

    public IAgentCapability? GetAgent(string id) => Find<IAgentCapability>(id);

    public IReadOnlyList<IAgentCapability> ListAgents() => _enabled.OfType<IAgentCapability>().ToArray();

    public IReadOnlyList<IAgentCapability> AllAgents()
        => _enabled.Concat(_builtIn).OfType<IAgentCapability>().ToArray();

    public IForgeCapability? GetForge(string id) => Find<IForgeCapability>(id);

    public IReadOnlyList<IForgeCapability> ListForges() => _enabled.OfType<IForgeCapability>().ToArray();

    public CapabilityReadinessDto? Readiness(string id)
    {
        if (_readiness.TryGetValue(id, out var readiness))
            return readiness;
        var enabled = _enabled.FirstOrDefault(module => Matches(module, id));
        if (enabled is not null)
            return new CapabilityReadinessDto(enabled.Identity.Id, enabled.Identity.DisplayName, true, [], []);
        var builtIn = _builtIn.FirstOrDefault(module => Matches(module, id));
        return builtIn is null
            ? null
            : new CapabilityReadinessDto(builtIn.Identity.Id, builtIn.Identity.DisplayName, false, [], []);
    }

    public static StubRuntimeCapability Dotnet() => new()
    {
        Identity = new CapabilityIdentity("dotnet", "1.0.0", ".NET", "agent-up"),
        ExtraAttributes =
        [
            new RuntimeAttributeSpec("project", true, "path"),
            new RuntimeAttributeSpec("run", false, "object"),
            new RuntimeAttributeSpec("arguments", false, "string")
        ]
    };

    public static StubRuntimeCapability Docker() => new()
    {
        Identity = new CapabilityIdentity("docker", "1.0.0", "Docker", "agent-up"),
        ExtraAttributes = [new RuntimeAttributeSpec("image", true, "string")]
    };

    public static StubAgentCapability Codex() => Agent("codex", "Codex", "codex-acp");

    public static StubAgentCapability Cursor() => Agent("cursor", "Cursor", "agent", ["acp"]);

    public static StubAgentCapability Claude() => Agent("claude", "Claude", "claude-agent-acp");

    private static StubAgentCapability Agent(string id, string displayName, string command, IReadOnlyList<string>? arguments = null)
        => new()
        {
            Identity = new CapabilityIdentity(id, "1.0.0", displayName, "agent-up"),
            LaunchResult = new AgentLaunchResult(command, arguments ?? [])
        };

    private FakeEnabledCapabilityPackages Replace(IEnumerable<ICapability> modules)
    {
        foreach (var module in modules)
        {
            _enabled.RemoveAll(existing => Matches(existing, module.Identity.Id));
            _enabled.Add(module);
        }

        return this;
    }

    private T? Find<T>(string id) where T : class, ICapability
        => _enabled.OfType<T>().FirstOrDefault(module => Matches(module, id));

    private static bool Matches(ICapability module, string id)
        => module.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase);
}
