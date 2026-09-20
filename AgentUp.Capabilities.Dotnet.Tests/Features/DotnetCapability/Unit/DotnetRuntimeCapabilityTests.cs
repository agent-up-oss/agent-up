using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

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
