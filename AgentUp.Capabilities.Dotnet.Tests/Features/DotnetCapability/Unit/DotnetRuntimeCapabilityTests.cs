using System.Text.Json;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;
using AgentUp.Sdk.Runtime;

namespace AgentUp.Capabilities.Dotnet.Tests.Features.DotnetCapability.Unit;

[TestFixture]
public sealed class DotnetRuntimeCapabilityTests
{
    [Test]
    public void Deliver_maps_sdk_10_to_the_dotnet_sdk_10_nix_package()
    {
        var result = new DotnetRuntimeCapability().Deliver("10.0.x");

        Assert.That(result.CanDeliver, Is.True);
        Assert.That(result.NixPackage, Is.EqualTo("dotnet-sdk_10"));
    }

    [Test]
    public void Deliver_rejects_an_unknown_technology_version()
    {
        var result = new DotnetRuntimeCapability().Deliver("7.0.x");

        Assert.That(result.CanDeliver, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("7.0.x"));
    }

    [TestCase(null, "dotnet-sdk_10")]
    [TestCase("", "dotnet-sdk_10")]
    [TestCase("9.0.x", "dotnet-sdk_9")]
    [TestCase("8.0", "dotnet-sdk_8")]
    public void Deliver_maps_each_supported_sdk_line_to_its_nix_package(string? sdk, string expected)
    {
        var result = new DotnetRuntimeCapability().Deliver(sdk);

        Assert.That(result.CanDeliver, Is.True);
        Assert.That(result.NixPackage, Is.EqualTo(expected));
    }

    // The technology version is an input to Deliver, so an application asking for an SDK this
    // module cannot provide must not reach Host at all.
    [Test]
    public void Host_refuses_a_technology_version_the_module_cannot_deliver()
    {
        var result = new DotnetRuntimeCapability().Host(Request("7.0.x", new Dictionary<string, string>
        {
            ["project"] = "Api.csproj"
        }));

        Assert.That(result.CanRun, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("7.0.x"));
    }

    [Test]
    public void Host_requires_a_project()
    {
        var result = new DotnetRuntimeCapability().Host(Request("10.0.x", new Dictionary<string, string>()));

        Assert.That(result.CanRun, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("project"));
    }

    [Test]
    public void Bind_rejects_an_item_with_no_project()
    {
        var result = new DotnetRuntimeCapability().Bind([new Dictionary<string, JsonElement>
        {
            ["name"] = JsonDocument.Parse("\"Example API\"").RootElement
        }]);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("project"));
    }

    // `run` is how the repository's own agent-up.json spells it, so the binder has to see the
    // project through that nesting rather than report it missing.
    [Test]
    public void Bind_accepts_a_project_nested_under_run()
    {
        var result = new DotnetRuntimeCapability().Bind([new Dictionary<string, JsonElement>
        {
            ["name"] = JsonDocument.Parse("\"Example API\"").RootElement,
            ["run"] = JsonDocument.Parse("""{"project":"ExampleApi.csproj"}""").RootElement
        }]);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Items.Single()["project"], Is.EqualTo("ExampleApi.csproj"));
    }

    private static RuntimeHostRequest Request(string? sdk, IReadOnlyDictionary<string, string> parameters)
        => new(
            "api",
            sdk,
            parameters,
            new Dictionary<string, string>(),
            [],
            [],
            [],
            "workspace",
            "container");

    [Test]
    public void Host_runs_the_project_through_dotnet()
    {
        var result = new DotnetRuntimeCapability().Host(new(
            "api",
            "10.0.x",
            new Dictionary<string, string> { ["project"] = "Api.csproj" },
            new Dictionary<string, string>(),
            [],
            [],
            ["--no-launch-profile"],
            "workspace",
            "container"));

        Assert.That(result.CanRun, Is.True);
        Assert.That(result.FileName, Is.EqualTo("dotnet"));
        Assert.That(result.Arguments, Is.EqualTo(new[] { "run", "--project", "Api.csproj", "--no-launch-profile" }));
        Assert.That(result.NixPackage, Is.EqualTo("dotnet-sdk_10"));
    }
}
