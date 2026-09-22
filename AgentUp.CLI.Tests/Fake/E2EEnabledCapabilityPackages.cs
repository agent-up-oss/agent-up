using System.Text.Json;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class E2EEnabledCapabilityPackages : IEnabledCapabilityPackages
{
    private readonly IReadOnlyList<IRuntimeCapability> _runtimes =
    [
        Runtime("dotnet", [new RuntimeAttributeSpec("project", true, "path"), new RuntimeAttributeSpec("run", false, "object"), new RuntimeAttributeSpec("arguments", false, "string")]),
        Runtime("docker", [new RuntimeAttributeSpec("image", true, "string"), new RuntimeAttributeSpec("volumes", false, "array"), new RuntimeAttributeSpec("command", false, "array")]),
        Runtime("python", [new RuntimeAttributeSpec("script", true, "path")])
    ];

    public CapabilityPackageManifest? GetEnabled(string id) => null;

    public CapabilityLaunchPlan? AgentLaunch(string id) => null;

    public CapabilityLaunchWrapDto WrapLaunch(string fileName, IReadOnlyList<string> arguments)
        => new(fileName, arguments);

    public CapabilityLaunchWrapDto WrapModule(string id, string fileName, IReadOnlyList<string> arguments)
        => new(fileName, arguments);

    public IRuntimeCapability? GetRuntime(string id)
        => _runtimes.FirstOrDefault(runtime => runtime.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<IRuntimeCapability> ListRuntimes() => _runtimes;

    public IAgentCapability? GetAgent(string id) => null;

    public IReadOnlyList<IAgentCapability> ListAgents() => [];

    private static E2ERuntimeCapability Runtime(string id, IReadOnlyList<RuntimeAttributeSpec> extras)
        => new(id, extras);

    private sealed class E2ERuntimeCapability(string id, IReadOnlyList<RuntimeAttributeSpec> extras) : IRuntimeCapability
    {
        public CapabilityIdentity Identity { get; } = new(id, "1.0.0", id, "agent-up");

        public string SectionName => id;

        public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [];

        public IReadOnlyList<RuntimeAttributeSpec> ExtraAttributes { get; } = extras;

        public RuntimeBindResult Bind(IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> items)
            => RuntimeSectionBinder.Bind(items, ExtraAttributes);

        public RuntimeDeliverResult Deliver(string? technologyVersion)
            => new(true, id, []);

        public RuntimeHostResult Host(RuntimeHostRequest app)
        {
            if (id == "docker")
            {
                app.Parameters.TryGetValue("image", out var image);
                return new RuntimeHostResult(true, "docker", ["run", "-d", "--name", app.ContainerName, image ?? ""], []);
            }

            if (id == "python")
            {
                app.Parameters.TryGetValue("script", out var script);
                return new RuntimeHostResult(true, "python", [script ?? ""], []);
            }

            app.Parameters.TryGetValue("project", out var project);
            var arguments = new List<string> { "run", "--project", project ?? "" };
            arguments.AddRange(app.ExtraArguments);
            return new RuntimeHostResult(true, "dotnet", arguments, []);
        }
    }
}
