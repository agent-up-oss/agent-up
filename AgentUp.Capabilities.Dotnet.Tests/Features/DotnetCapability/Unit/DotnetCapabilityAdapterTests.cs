using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Interfaces;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

namespace AgentUp.Capabilities.Dotnet.Tests.Features.DotnetCapability.Unit;

[TestFixture]
public sealed class DotnetCapabilityAdapterTests
{
    [Test]
    public async Task ValidateAsync_acceptsMatchingSdkBand()
    {
        var adapter = new DotnetCapabilityAdapter(new FakeDotnetVersionProvider("10.0.101"));
        var declaration = new CapabilityDeclaration(
            "api",
            "dotnet",
            new Dictionary<string, string> { ["sdk"] = "10.0.x" },
            new Dictionary<string, string> { ["project"] = "src/Api/Api.csproj" });

        var result = await adapter.ValidateAsync(declaration, await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.That(result.CanRun, Is.True);
    }

    [Test]
    public async Task CreateLaunchPlanAsync_usesDotnetRunProject()
    {
        var adapter = new DotnetCapabilityAdapter(new FakeDotnetVersionProvider("10.0.101"));
        var declaration = new CapabilityDeclaration(
            "api",
            "dotnet",
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["project"] = "src/Api/Api.csproj", ["arguments"] = "--no-launch-profile" });

        var plan = await adapter.CreateLaunchPlanAsync(declaration, [], CancellationToken.None);

        Assert.That(plan.Command, Is.EqualTo("dotnet run --project src/Api/Api.csproj --no-launch-profile"));
    }

    private sealed class FakeDotnetVersionProvider(params string[] versions) : IDotnetVersionProvider
    {
        public Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CapabilityInstalledVersion>>(versions
                .Select(version => new CapabilityInstalledVersion("dotnet", version, "dotnet", CapabilityVersionSource.System, false))
                .ToList());
    }
}
