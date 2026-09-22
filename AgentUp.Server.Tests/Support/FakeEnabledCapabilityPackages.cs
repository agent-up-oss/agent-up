using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Tests.Support;

internal sealed class FakeEnabledCapabilityPackages : IEnabledCapabilityPackages
{
    private readonly Dictionary<string, CapabilityPackageManifest> _enabled;
    private IReadOnlyList<IAgentCapability> _agents = [];
    private readonly Dictionary<string, IRuntimeCapability> _runtimes = new(StringComparer.OrdinalIgnoreCase);

    public FakeEnabledCapabilityPackages(params CapabilityPackageManifest[] packages)
    {
        _enabled = packages.ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
    }

    public FakeEnabledCapabilityPackages WithAgents(params IAgentCapability[] agents)
    {
        _agents = agents;
        return this;
    }

    public FakeEnabledCapabilityPackages WithRuntimes(params IRuntimeCapability[] runtimes)
    {
        foreach (var runtime in runtimes)
            _runtimes[runtime.Identity.Id] = runtime;
        return this;
    }

    public static FakeEnabledCapabilityPackages FirstParty()
        => new(Dotnet(), Docker(), Codex(), Cursor(), Claude());

    public CapabilityPackageManifest? GetEnabled(string id)
        => _enabled.GetValueOrDefault(id);

    public CapabilityLaunchPlan? AgentLaunch(string id)
    {
        var manifest = GetEnabled(id);
        if (manifest?.Launch is null || string.IsNullOrWhiteSpace(manifest.Launch.Command))
            return null;
        return new CapabilityLaunchPlan(manifest.Launch.Command, Arguments: manifest.Launch.Arguments);
    }

    public CapabilityLaunchWrapDto WrapLaunch(string fileName, IReadOnlyList<string> arguments)
        => new(fileName, arguments);

    public CapabilityLaunchWrapDto WrapModule(string id, string fileName, IReadOnlyList<string> arguments)
        => new(fileName, arguments);

    public IRuntimeCapability? GetRuntime(string id)
        => _runtimes.GetValueOrDefault(id);

    public IReadOnlyList<IRuntimeCapability> ListRuntimes() => _runtimes.Values.ToArray();

    public IAgentCapability? GetAgent(string id)
        => _agents.FirstOrDefault(agent => agent.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<IAgentCapability> ListAgents() => _agents;

    public static CapabilityPackageManifest Dotnet() => new()
    {
        Id = "dotnet",
        Version = "1.0.0",
        DisplayName = ".NET",
        Publisher = "agent-up",
        Kind = "runtime",
        Parameters = new Dictionary<string, CapabilityParameterSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["project"] = new() { Required = true, Type = "path" }
        },
        Launch = new CapabilityLaunchTemplate
        {
            Command = "dotnet",
            Arguments = ["run", "--project", "{{parameters.project}}"]
        }
    };

    public static CapabilityPackageManifest Docker() => new()
    {
        Id = "docker",
        Version = "1.0.0",
        DisplayName = "Docker",
        Publisher = "agent-up",
        Kind = "runtime",
        Parameters = new Dictionary<string, CapabilityParameterSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["image"] = new() { Required = true, Type = "string" }
        }
    };

    public static CapabilityPackageManifest Codex() => Agent("codex", "codex-acp");

    public static CapabilityPackageManifest Cursor() => Agent("cursor", "agent", ["acp"]);

    public static CapabilityPackageManifest Claude() => Agent("claude", "claude-agent-acp");

    private static CapabilityPackageManifest Agent(string id, string command, IReadOnlyList<string>? arguments = null)
        => new()
        {
            Id = id,
            Version = "1.0.0",
            DisplayName = id,
            Publisher = "agent-up",
            Kind = "agent",
            Launch = new CapabilityLaunchTemplate { Command = command, Arguments = arguments ?? [] }
        };
}
