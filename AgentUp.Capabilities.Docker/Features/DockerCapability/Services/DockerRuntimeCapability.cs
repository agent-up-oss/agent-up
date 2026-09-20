using System.Text.Json;
using AgentUp.Sdk.Common;
using AgentUp.Sdk.Runtime;

namespace AgentUp.Capabilities.Docker.Features.DockerCapability.Services;

public sealed class DockerRuntimeCapability : IRuntimeCapability
{
    public const string PackageId = "docker";
    public const string PackageVersion = "1.0.0";
    public const string NixPackage = "docker";

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, "Docker", "agent-up");

    public string SectionName => PackageId;

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [new(NixPackage)];

    public IReadOnlyList<RuntimeAttributeSpec> ExtraAttributes { get; } =
    [
        new("image", true, "string"),
        new("volumes", false, "array"),
        new("command", false, "array")
    ];

    public RuntimeBindResult Bind(IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> items)
        => RuntimeSectionBinder.Bind(items, ExtraAttributes);

    public RuntimeDeliverResult Deliver(string? technologyVersion)
        => new(true, NixPackage, []);

    public RuntimeHostResult Host(RuntimeHostRequest app)
    {
        if (!app.Parameters.TryGetValue("image", out var image) || string.IsNullOrWhiteSpace(image))
            return new RuntimeHostResult(false, "", [], [$"Capability '{PackageId}' requires parameter 'image'."]);

        var arguments = new List<string> { "run", "-d", "--name", app.ContainerName, "--add-host", "host.agent-up:host-gateway" };
        foreach (var port in app.Ports)
        {
            arguments.Add("-p");
            arguments.Add($"{port.AllocatedPort}:{port.DefaultPort}");
        }

        foreach (var environmentFile in app.EnvironmentFilePaths ?? [])
        {
            arguments.Add("--env-file");
            arguments.Add(environmentFile);
        }

        foreach (var (key, value) in app.Environment)
        {
            arguments.Add("-e");
            arguments.Add($"{key}={value}");
        }

        if (!string.IsNullOrWhiteSpace(app.AuditEndpoint))
        {
            arguments.Add("-e");
            arguments.Add($"AGENT_UP_AUDIT_ENDPOINT={RewriteLoopback(app.AuditEndpoint)}");
        }

        arguments.Add("-e");
        arguments.Add($"AGENT_UP_WORKSPACE_ID={app.WorkspaceId}");
        arguments.Add("-e");
        arguments.Add($"AGENT_UP_APPLICATION={app.Name}");

        foreach (var volume in app.Volumes)
        {
            arguments.Add("-v");
            arguments.Add(volume);
        }

        arguments.Add(image);
        arguments.AddRange(app.ExtraArguments);
        return new RuntimeHostResult(true, "docker", arguments, [], NixPackage);
    }

    private static string RewriteLoopback(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback)
            return endpoint;
        return new UriBuilder(uri) { Host = "host.agent-up" }.Uri.AbsoluteUri;
    }
}
