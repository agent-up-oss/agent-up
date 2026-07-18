using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Services;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityDistribution.Provider;

[TestFixture]
public sealed class CapabilityInstallPlannerTests
{
    [Test]
    public void Plan_placesArtifactUnderToolCache()
    {
        var planner = new CapabilityInstallPlanner(new CapabilityToolCacheLayout(Path.Join(Path.GetTempPath(), "agent-up-capabilities")));
        var artifact = new CapabilityArtifact(
            "dotnet",
            "10.0.100",
            new Uri("https://github.com/example/releases/dotnet.zip"),
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        var plan = planner.Plan(artifact);

        Assert.That(plan.DownloadPath, Does.Contain(Path.Join("downloads", "dotnet", "10.0.100")));
        Assert.That(plan.InstallDirectory, Does.Contain(Path.Join("capabilities", "dotnet", "10.0.100")));
        Assert.That(plan.RegistrationPath, Does.Contain(Path.Join("registry", "dotnet")));
    }
}
