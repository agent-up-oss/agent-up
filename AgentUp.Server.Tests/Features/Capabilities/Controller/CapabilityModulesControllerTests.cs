using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
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
    public void List_names_nix_when_an_enabled_module_cannot_run()
    {
        var listed = new CapabilityModulesController(
            CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true)).List().Single();

        Assert.Multiple(() =>
        {
            Assert.That(listed.State, Is.EqualTo("error"));
            Assert.That(listed.CanRun, Is.False);
            Assert.That(listed.Messages, Has.Some.Contains("https://nixos.org/download"));
            Assert.That(listed.Messages, Has.None.Contains("is not enabled"));
        });
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

    [Test]
    public void ListForges_returns_loaded_forge_modules()
    {
        var forge = new StubForgeCapability();
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService(
            enabled: true,
            manifest: new CapabilityPackageManifest
            {
                SchemaVersion = "1",
                Id = "github",
                Version = "1.0.0",
                DisplayName = "GitHub",
                Publisher = "agent-up",
                Kind = "forge"
            },
            loader: new FixedCapabilityModuleLoader(new CapabilityLoadedModule(null, null, forge))));

        Assert.That(controller.ListForges(), Is.EqualTo(new[] { forge }));
        Assert.That(controller.GetForge("github"), Is.SameAs(forge));
    }
}
