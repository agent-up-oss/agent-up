using System.Text.Json;
using AgentUp.Sdk.Common;
using AgentUp.Sdk.Runtime;

namespace AgentUp.Server.Tests.Support;

internal sealed class StubRuntimeCapability : IRuntimeCapability
{
    public CapabilityIdentity Identity { get; init; } = new("dotnet", "1.0.0", ".NET", "agent-up");

    public string SectionName => Identity.Id;

    public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [];

    public IReadOnlyList<RuntimeAttributeSpec> ExtraAttributes { get; init; } = [];

    public bool CanDeliver { get; init; } = true;

    public RuntimeBindResult Bind(IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> items)
        => RuntimeSectionBinder.Bind(items, ExtraAttributes);

    public RuntimeDeliverResult Deliver(string? technologyVersion)
        => new(CanDeliver, CanDeliver ? "dotnet-sdk_10" : null, CanDeliver ? [] : ["cannot deliver"]);

    public RuntimeHostResult Host(RuntimeHostRequest app)
    {
        if (!CanDeliver)
            return new RuntimeHostResult(false, "", [], ["cannot host"]);
        if (Identity.Id == "docker")
        {
            app.Parameters.TryGetValue("image", out var image);
            return new RuntimeHostResult(true, "docker", ["run", "-d", "--name", app.ContainerName, image ?? ""], []);
        }

        app.Parameters.TryGetValue("project", out var project);
        var arguments = new List<string> { "run", "--project", project ?? "" };
        arguments.AddRange(app.ExtraArguments);
        return new RuntimeHostResult(true, "dotnet", arguments, []);
    }
}
