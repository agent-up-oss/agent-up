using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Capabilities.Controller;

[TestFixture]
public sealed class CapabilityModulesControllerTests
{
    [Test]
    public void List_returns_local_registry_modules()
    {
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService());

        var listed = controller.List();

        Assert.That(listed.Single().Id, Is.EqualTo("dotnet"));
        Assert.That(listed.Single().Enabled, Is.False);
    }

    [Test]
    public void Enable_then_disable_updates_the_catalog_state()
    {
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService(nixAvailable: true));

        var enabled = controller.Enable("dotnet", "1.0.0");
        Assert.That(enabled.Enabled, Is.True);
        Assert.That(enabled.CanRun, Is.EqualTo(!OperatingSystem.IsWindows()));

        var disabled = controller.Disable("dotnet");
        Assert.That(disabled.Enabled, Is.False);
        Assert.That(controller.List().Single().Enabled, Is.False);
    }

    [Test]
    public void WrapModule_returns_the_unwrapped_command_when_nix_is_missing()
    {
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true));

        var wrap = controller.WrapModule("dotnet", "dotnet", ["run"]);

        Assert.That(wrap.FileName, Is.EqualTo("dotnet"));
        Assert.That(wrap.Arguments, Is.EqualTo(new[] { "run" }));
    }

    [Test]
    public void GetRuntime_returns_the_loaded_module()
    {
        var runtime = new StubRuntimeCapability();
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService(
            enabled: true,
            loader: new FixedCapabilityModuleLoader(new CapabilityLoadedModule(runtime, null))));

        Assert.That(controller.GetRuntime("dotnet"), Is.SameAs(runtime));
        Assert.That(controller.ListAgents(), Is.Empty);
    }
}
