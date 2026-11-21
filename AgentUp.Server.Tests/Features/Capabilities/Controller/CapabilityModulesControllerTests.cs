using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Capabilities.Controller;

[TestFixture]
public sealed class CapabilityModulesControllerTests
{
    [Test]
    public void List_returns_the_built_in_capabilities()
    {
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService());

        Assert.That(controller.List().Single().Id, Is.EqualTo("dotnet"));
    }

    [Test]
    public void Enable_then_disable_updates_the_capability_state()
    {
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService());

        Assert.That(controller.Enable("dotnet").Module!.CanRun, Is.True);
        Assert.That(controller.Disable("dotnet").Module!.Enabled, Is.False);
    }

    [Test]
    public void Enable_reports_missing_software_instead_of_enabling()
    {
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService(nixAvailable: false));

        Assert.That(controller.Enable("dotnet").Status, Is.EqualTo(CapabilityChangeStatus.Unavailable));
    }

    [Test]
    public void UpdateSettings_reaches_the_service()
    {
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService());

        Assert.That(controller.UpdateSettings("dotnet", new Dictionary<string, string?> { ["x"] = "y" }).Status, Is.EqualTo(CapabilityChangeStatus.Invalid));
    }

    [Test]
    public void GetRuntime_returns_an_enabled_built_in_runtime()
    {
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService(enabled: true));

        Assert.That(controller.GetRuntime("dotnet")!.Identity.Id, Is.EqualTo("dotnet"));
    }

    [Test]
    public void WrapModule_returns_the_unwrapped_command_when_nix_is_missing()
    {
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true));

        Assert.That(controller.WrapModule("dotnet", "dotnet", ["run"]).FileName, Is.EqualTo("dotnet"));
    }
}
