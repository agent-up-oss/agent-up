using AgentUp.Capabilities.Common.Features.NixRuntime.Controllers;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Providers;
using AgentUp.Capabilities.Common.Features.NixRuntime.Services;

namespace AgentUp.Capabilities.Common.Tests.Features.NixRuntime.Controller;

[TestFixture]
public sealed class NixCapabilityEnvironmentControllerTests
{
    [Test]
    public void Wrap_returns_the_service_contract()
    {
        var controller = new NixCapabilityEnvironmentController(
            new NixCapabilityEnvironmentService(new NixCapabilityCommandBuilder(), new CapabilityIndexMergeProvider()));

        var wrap = controller.Wrap("docker", ["ps"], new NixEnvironmentSpec(null, "rev", ["docker"]));

        Assert.That(wrap.FileName, Is.EqualTo("nix"));
    }

    [Test]
    public void Wrap_passes_through_an_empty_environment()
    {
        var controller = new NixCapabilityEnvironmentController(
            new NixCapabilityEnvironmentService(new NixCapabilityCommandBuilder(), new CapabilityIndexMergeProvider()));

        var wrap = controller.Wrap("dotnet", ["run"], new NixEnvironmentSpec(null, null, []));

        Assert.That(wrap.FileName, Is.EqualTo("dotnet"));
    }
}
