using System.Text.Json;
using AgentUp.Sdk.Common;
using AgentUp.Sdk.Runtime;

namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

public sealed class DotnetRuntimeCapability : IRuntimeCapability
{
    public const string PackageId = "dotnet";
    public const string PackageVersion = "1.0.0";
    public const string DefaultNixPackage = "dotnet-sdk_10";

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, ".NET", "agent-up");

    public string SectionName => PackageId;

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [new(DefaultNixPackage)];

    public IReadOnlyList<RuntimeAttributeSpec> ExtraAttributes { get; } =
    [
        new("project", true, "path"),
        new("run", false, "object"),
        new("arguments", false, "string")
    ];

    public RuntimeBindResult Bind(IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> items)
        => RuntimeSectionBinder.Bind(items, ExtraAttributes);

    public RuntimeDeliverResult Deliver(string? technologyVersion)
    {
        var nixPackage = ResolveNixPackage(technologyVersion);
        if (nixPackage is null)
        {
            return new RuntimeDeliverResult(
                false,
                null,
                [$"Capability '{PackageId}' cannot deliver SDK '{technologyVersion}'."]);
        }

        return new RuntimeDeliverResult(true, nixPackage, []);
    }

    public RuntimeHostResult Host(RuntimeHostRequest app)
    {
        var delivered = Deliver(app.TechnologyVersion);
        if (!delivered.CanDeliver)
            return new RuntimeHostResult(false, "", [], delivered.Messages);

        if (!app.Parameters.TryGetValue("project", out var project) || string.IsNullOrWhiteSpace(project))
            return new RuntimeHostResult(false, "", [], [$"Capability '{PackageId}' requires parameter 'project'."]);

        var arguments = new List<string> { "run", "--project", project };
        arguments.AddRange(app.ExtraArguments);
        return new RuntimeHostResult(true, "dotnet", arguments, [], delivered.NixPackage);
    }

    public static string? ResolveNixPackage(string? technologyVersion)
    {
        if (string.IsNullOrWhiteSpace(technologyVersion))
            return DefaultNixPackage;

        var value = technologyVersion.Trim();
        if (value.StartsWith("10", StringComparison.Ordinal))
            return "dotnet-sdk_10";
        if (value.StartsWith("9", StringComparison.Ordinal))
            return "dotnet-sdk_9";
        if (value.StartsWith("8", StringComparison.Ordinal))
            return "dotnet-sdk_8";
        return null;
    }
}
