using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityModuleServiceTests
{
    [Test]
    public void List_shows_every_built_in_capability_whether_or_not_it_is_enabled()
    {
        var service = CapabilityModuleHarness.CreateService(modules: [CapabilityModuleHarness.Dotnet(), Forge()]);

        var listed = service.List();

        Assert.That(listed.Select(module => module.Id), Is.EqualTo(new[] { "dotnet", "github" }));
        Assert.That(listed.All(module => !module.Enabled), Is.True);
    }

    [Test]
    public void A_disabled_capability_whose_software_is_found_can_be_enabled()
    {
        var listed = CapabilityModuleHarness.CreateService().List().Single();

        Assert.That(listed.State, Is.EqualTo(CapabilityModuleStates.Disabled));
        Assert.That(listed.Available, Is.True);
        Assert.That(listed.Messages, Has.Some.Contains("capabilities bootstrap"));
    }

    [Test]
    public void Enable_then_disable_round_trips_the_enabled_set()
    {
        var service = CapabilityModuleHarness.CreateService();

        var enabled = service.Enable("dotnet");
        Assert.That(enabled.Module!.State, Is.EqualTo(CapabilityModuleStates.Ready));
        Assert.That(service.GetRuntime("dotnet"), Is.Not.Null);

        var disabled = service.Disable("dotnet");
        Assert.That(disabled.Module!.Enabled, Is.False);
        Assert.That(service.GetRuntime("dotnet"), Is.Null);
    }

    [Test]
    public void Enable_is_refused_while_nix_is_missing_for_a_capability_nix_delivers()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: false);

        var result = service.Enable("dotnet");

        Assert.That(result.Status, Is.EqualTo(CapabilityChangeStatus.Unavailable));
        Assert.That(result.Errors, Has.Some.Contains("https://nixos.org/download"));
        Assert.That(service.List().Single().Enabled, Is.False);
    }

    [Test]
    public void Enable_is_refused_while_an_executable_is_not_on_the_search_path()
    {
        var service = CapabilityModuleHarness.CreateService(modules: [Agent()]);

        var result = service.Enable("codex");

        Assert.That(result.Status, Is.EqualTo(CapabilityChangeStatus.Unavailable));
        Assert.That(result.Module!.Missing, Is.EqualTo(new[] { "codex-acp" }));
        Assert.That(result.Module.State, Is.EqualTo(CapabilityModuleStates.Unavailable));
    }

    [Test]
    public void Enable_succeeds_once_the_executable_is_found()
    {
        var service = CapabilityModuleHarness.CreateService(modules: [Agent()], onPath: ["codex-acp"]);

        Assert.That(service.Enable("codex").Status, Is.EqualTo(CapabilityChangeStatus.Changed));
        Assert.That(service.ListAgents().Single().Identity.Id, Is.EqualTo("codex"));
    }

    [Test]
    public void Enable_names_an_id_the_server_was_not_built_with()
    {
        var result = CapabilityModuleHarness.CreateService().Enable("python");

        Assert.That(result.Status, Is.EqualTo(CapabilityChangeStatus.NotFound));
        Assert.That(result.Errors.Single(), Does.Contain("python"));
    }

    [Test]
    public void An_enabled_capability_whose_software_disappears_is_unavailable_and_cannot_run()
    {
        var listed = CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true).List().Single();

        Assert.That(listed.Enabled, Is.True);
        Assert.That(listed.State, Is.EqualTo(CapabilityModuleStates.Unavailable));
        Assert.That(listed.CanRun, Is.False);
        Assert.That(listed.Missing, Is.EqualTo(new[] { "nix" }));
    }

    [Test]
    public void A_forge_needs_nothing_and_is_ready_once_enabled_even_without_nix()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true, modules: [Forge()]);

        Assert.That(service.List().Single().State, Is.EqualTo(CapabilityModuleStates.Ready));
        Assert.That(service.ListForges(), Has.Count.EqualTo(1));
    }

    [Test]
    public void An_enabled_capability_with_an_unset_required_setting_needs_setup()
    {
        var service = CapabilityModuleHarness.CreateService(enabled: true, modules: [Configurable()]);

        var listed = service.List().Single();

        Assert.That(listed.State, Is.EqualTo(CapabilityModuleStates.NeedsSetup));
        Assert.That(listed.Messages.Single(), Does.Contain("Endpoint"));
        Assert.That(service.Readiness("opencode")!.MissingSettings, Is.EqualTo(new[] { "Endpoint" }));
    }

    [Test]
    public void UpdateSettings_stores_values_and_makes_the_capability_ready()
    {
        var service = CapabilityModuleHarness.CreateService(enabled: true, modules: [Configurable()]);

        var result = service.UpdateSettings("opencode", new Dictionary<string, string?> { ["endpoint"] = " http://localhost:11434 " });

        Assert.That(result.Module!.State, Is.EqualTo(CapabilityModuleStates.Ready));
        Assert.That(result.Module.Settings.Single(setting => setting.Key == "endpoint").Value, Is.EqualTo("http://localhost:11434"));
    }

    [Test]
    public void UpdateSettings_never_returns_a_secret_but_says_one_is_stored()
    {
        var service = CapabilityModuleHarness.CreateService(enabled: true, modules: [Configurable()]);

        var token = service.UpdateSettings("opencode", new Dictionary<string, string?> { ["token"] = "sk-secret" })
            .Module!.Settings.Single(setting => setting.Key == "token");

        Assert.That(token.Value, Is.Null);
        Assert.That(token.HasValue, Is.True);
    }

    [Test]
    public void UpdateSettings_keeps_a_value_the_request_leaves_out_and_clears_an_empty_one()
    {
        var settings = new CapabilityModuleHarness.MemorySettings();
        var service = CapabilityModuleHarness.CreateService(enabled: true, modules: [Configurable()], settings: settings);
        service.UpdateSettings("opencode", new Dictionary<string, string?> { ["endpoint"] = "http://a.test", ["token"] = "sk" });

        service.UpdateSettings("opencode", new Dictionary<string, string?> { ["endpoint"] = "" });

        Assert.That(settings.Read("opencode").ContainsKey("endpoint"), Is.False);
        Assert.That(settings.Read("opencode")["token"], Is.EqualTo("sk"));
    }

    [Test]
    public void UpdateSettings_rejects_an_unknown_key_and_a_url_that_is_not_http()
    {
        var service = CapabilityModuleHarness.CreateService(enabled: true, modules: [Configurable()]);

        var result = service.UpdateSettings(
            "opencode",
            new Dictionary<string, string?> { ["endpoint"] = "file:///etc/passwd", ["colour"] = "green" });

        Assert.That(result.Status, Is.EqualTo(CapabilityChangeStatus.Invalid));
        Assert.That(result.Errors, Has.Count.EqualTo(2));
        Assert.That(result.Module!.Settings.Single(setting => setting.Key == "endpoint").HasValue, Is.False);
    }

    [Test]
    public void AgentLaunch_passes_the_stored_settings_and_the_launch_environment()
    {
        var agent = Configurable();
        var service = CapabilityModuleHarness.CreateService(enabled: true, modules: [agent], nixAvailable: false);
        service.UpdateSettings("opencode", new Dictionary<string, string?> { ["endpoint"] = "http://a.test" });

        var plan = service.AgentLaunch("opencode")!;

        Assert.That(agent.LaunchedWith!["endpoint"], Is.EqualTo("http://a.test"));
        Assert.That(plan.Command, Is.EqualTo("opencode"));
        Assert.That(plan.Environment!["OPENCODE_CONFIG_CONTENT"], Is.EqualTo("{}"));
    }

    [Test]
    public void AgentLaunch_wraps_the_agent_and_every_enabled_runtime_in_one_pinned_shell()
    {
        var service = CapabilityModuleHarness.CreateService(enabled: true, modules: [Agent(), CapabilityModuleHarness.Dotnet()], onPath: ["codex-acp"]);

        var plan = service.AgentLaunch("codex")!;

        Assert.That(plan.Command, Is.EqualTo("nix-shell"));
        Assert.That(plan.Arguments![0], Is.EqualTo("/data/capabilities/nix/codex+dotnet/default.nix"));
        Assert.That(plan.Arguments[^1], Is.EqualTo("'codex-acp'"));
    }

    [Test]
    public void AgentLaunch_is_unwrapped_when_nix_is_missing()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true, modules: [Agent()]);

        Assert.That(service.AgentLaunch("codex")!.Command, Is.EqualTo("codex-acp"));
    }

    [Test]
    public void WrapLaunch_wraps_a_command_an_enabled_runtime_provides()
    {
        var service = CapabilityModuleHarness.CreateService(enabled: true);

        var wrapped = service.WrapLaunch("dotnet", ["run"]);

        Assert.That(wrapped.FileName, Is.EqualTo("nix-shell"));
        Assert.That(wrapped.Arguments, Is.EqualTo(new[] { "/data/capabilities/nix/dotnet/default.nix", "--run", "'dotnet' 'run'" }));
    }

    [Test]
    public void WrapLaunch_leaves_a_command_no_enabled_runtime_provides()
    {
        var service = CapabilityModuleHarness.CreateService(enabled: true);

        Assert.That(service.WrapLaunch("npm", ["install"]).FileName, Is.EqualTo("npm"));
        Assert.That(CapabilityModuleHarness.CreateService(enabled: false).WrapLaunch("dotnet", ["run"]).FileName, Is.EqualTo("dotnet"));
    }

    [Test]
    public void WrapModule_leaves_the_command_unchanged_when_nix_is_missing()
    {
        var service = CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true);

        Assert.That(service.WrapModule("dotnet", "dotnet", ["run"]).FileName, Is.EqualTo("dotnet"));
    }

    [Test]
    public void AllAgents_lists_built_in_agents_that_are_not_enabled()
    {
        var service = CapabilityModuleHarness.CreateService(modules: [Agent(), CapabilityModuleHarness.Dotnet()]);

        Assert.That(service.AllAgents().Single().Identity.Id, Is.EqualTo("codex"));
        Assert.That(service.ListAgents(), Is.Empty);
    }

    private static StubAgentCapability Agent() => new()
    {
        Software = [new CapabilitySoftware("node", DeliveredByNix: true), new CapabilitySoftware("codex-acp", DeliveredByNix: false)],
        NixPackages = [new NixPackageDeclaration("nodejs_22")]
    };

    private static StubAgentCapability Configurable() => new()
    {
        Identity = new CapabilityIdentity("opencode", "1.0.0", "OpenCode", "agent-up"),
        Settings =
        [
            new CapabilitySettingSpec("endpoint", "Endpoint", CapabilitySettingKind.Url, Required: true),
            new CapabilitySettingSpec("token", "Token", CapabilitySettingKind.Secret)
        ],
        LaunchResult = new AgentLaunchResult("opencode", ["acp"], null, new Dictionary<string, string> { ["OPENCODE_CONFIG_CONTENT"] = "{}" })
    };

    private static StubForgeCapability Forge() => new();
}
