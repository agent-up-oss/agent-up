using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityModuleServiceTests
{
    [Test]
    public void WrapLaunch_wraps_enabled_toolchain_packages_through_nix_shell()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: true, enabled: true);

        var wrap = service.WrapLaunch("dotnet", ["run"]);

        Assert.That(wrap.FileName, Is.EqualTo("nix-shell"));
        Assert.That(wrap.Arguments, Does.Contain("--run"));
    }

    [Test]
    public void WrapLaunch_leaves_commands_no_enabled_runtime_provides()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: true, enabled: true);

        var wrap = service.WrapLaunch("npm", ["install"]);

        Assert.That(wrap.FileName, Is.EqualTo("npm"));
        Assert.That(wrap.Arguments, Is.EqualTo(new[] { "install" }));
    }

    [Test]
    public void WrapLaunch_leaves_commands_unchanged_when_nix_is_missing()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true);

        var wrap = service.WrapLaunch("dotnet", ["run"]);

        Assert.That(wrap.FileName, Is.EqualTo("dotnet"));
        Assert.That(wrap.Arguments, Is.EqualTo(new[] { "run" }));
    }

    [Test]
    public void AgentLaunch_wraps_an_enabled_agent_package_through_nix_shell()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: true, enabled: true, manifest: CodexManifest());

        var plan = service.AgentLaunch("codex");

        Assert.That(plan!.Command, Is.EqualTo("nix-shell"));
        Assert.That(plan.Arguments, Does.Contain("--run"));
        Assert.That(plan.Arguments, Does.Contain("'codex-acp'"));
    }

    [Test]
    public void AgentLaunch_leaves_the_command_unwrapped_when_nix_is_missing()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true, manifest: CodexManifest());

        var plan = service.AgentLaunch("codex");

        Assert.That(plan!.Command, Is.EqualTo("codex-acp"));
        Assert.That(plan.Arguments, Is.Empty);
    }

    [Test]
    public void WrapModule_wraps_only_the_named_runtime_package()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: true, enabled: true);

        var wrap = service.WrapModule("dotnet", "dotnet", ["run"]);

        Assert.That(wrap.FileName, Is.EqualTo("nix-shell"));
        Assert.That(wrap.Arguments, Does.Contain("--run"));
    }

    [Test]
    public void GetRuntime_returns_the_loaded_module()
    {
        var runtime = new StubRuntimeCapability();
        var service = CapabilityModuleHarness.CreateService(
            enabled: true,
            loader: new FixedCapabilityModuleLoader(new CapabilityLoadedModule(runtime, null)));

        Assert.That(service.GetRuntime("dotnet"), Is.SameAs(runtime));
        Assert.That(service.ListAgents(), Is.Empty);
    }

    [Test]
    public void ListAgents_returns_loaded_agent_modules()
    {
        var agent = new StubAgentCapability();
        var service = CapabilityModuleHarness.CreateService(
            enabled: true,
            manifest: CodexManifest(),
            loader: new FixedCapabilityModuleLoader(new CapabilityLoadedModule(null, agent)));

        Assert.That(service.ListAgents(), Is.EqualTo(new[] { agent }));
        Assert.That(service.GetAgent("codex"), Is.SameAs(agent));
    }

    [Test]
    public void ListRuntimes_returns_loaded_runtime_modules()
    {
        var runtime = new StubRuntimeCapability();
        var service = CapabilityModuleHarness.CreateService(
            enabled: true,
            loader: new FixedCapabilityModuleLoader(new CapabilityLoadedModule(runtime, null)));

        Assert.That(service.ListRuntimes(), Is.EqualTo(new[] { runtime }));
    }

    private static CapabilityPackageManifest CodexManifest()
        => new()
        {
            SchemaVersion = "1",
            Id = "codex",
            Version = "1.0.0",
            DisplayName = "Codex",
            Publisher = "agent-up",
            Kind = "agent",
            Launch = new CapabilityLaunchTemplate { Command = "codex-acp", Arguments = [] }
        };

    // Enablement is Server-owned state, and the catalog Desktop and Mobile render is this list.
    [Test]
    public void List_reports_a_registry_package_as_disabled_until_it_is_enabled()
    {
        var service = CapabilityModuleHarness.CreateService();

        var listed = service.List().Single();

        Assert.Multiple(() =>
        {
            Assert.That(listed.Id, Is.EqualTo("dotnet"));
            Assert.That(listed.Version, Is.EqualTo("1.0.0"));
            Assert.That(listed.Enabled, Is.False);
            Assert.That(listed.State, Is.EqualTo("disabled"));
            Assert.That(listed.CanRun, Is.False);
        });
    }

    [Test]
    public void Enable_then_Disable_round_trips_the_enabled_set()
    {
        var service = CapabilityModuleHarness.CreateService();

        var enabled = service.Enable("dotnet", "1.0.0");
        Assert.Multiple(() =>
        {
            Assert.That(enabled.Enabled, Is.True);
            Assert.That(service.GetEnabled("dotnet")?.Id, Is.EqualTo("dotnet"));
        });

        var disabled = service.Disable("dotnet");
        Assert.Multiple(() =>
        {
            Assert.That(disabled.Enabled, Is.False);
            Assert.That(service.GetEnabled("dotnet"), Is.Null);
        });
    }

    [Test]
    public void Enable_without_a_version_takes_the_one_in_the_registry()
    {
        var service = CapabilityModuleHarness.CreateService();

        Assert.That(service.Enable("dotnet", null).Version, Is.EqualTo("1.0.0"));
    }

    // Enablement names a package that has to be there, so an id the registry does not carry is an
    // error rather than an empty enabled set that fails much later at workspace start.
    [Test]
    public void Enable_rejects_a_package_the_local_registry_does_not_have()
    {
        var service = CapabilityModuleHarness.CreateService();

        Assert.Multiple(() =>
        {
            Assert.That(() => service.Enable("python", null), Throws.InvalidOperationException);
            Assert.That(() => service.Enable("dotnet", "9.9.9"), Throws.InvalidOperationException);
        });
    }

    [Test]
    public void GetEnabled_returns_nothing_for_a_package_that_is_not_enabled()
    {
        Assert.That(CapabilityModuleHarness.CreateService().GetEnabled("dotnet"), Is.Null);
    }

    [Test]
    public void List_explains_an_enabled_module_that_cannot_run_without_nix()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true);

        var listed = service.List().Single();

        Assert.Multiple(() =>
        {
            Assert.That(listed.Enabled, Is.True);
            Assert.That(listed.CanRun, Is.False);
            Assert.That(listed.State, Is.EqualTo("error"));
            Assert.That(listed.Messages, Is.Not.Empty);
        });
    }
}
