using AgentUp.CLI.Features.Capabilities.Providers;
using AgentUp.CLI.Features.Capabilities.Services;
using AgentUp.CLI.Shared.Providers;
using AgentUp.CLI.Tests.Fake;
using AgentUp.CLI.Tests.Support;

namespace AgentUp.CLI.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilitiesServiceTests
{
    [Test]
    public async Task Disable_rejectsAnUnknownId()
    {
        var api = new FakeCapabilityModulesApi();

        var result = await Service(api).RunAsync(["disable", CliDomain.UnknownCapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("not built into this Server"));
            Assert.That(api.DisabledId, Is.Null);
        });
    }

    [Test]
    public async Task Disable_disablesAnEnabledModule()
    {
        var api = new FakeCapabilityModulesApi { Modules = [CliDomain.Capability().Enabled().Build()] };

        var result = await Service(api).RunAsync(["disable", CliDomain.CapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Does.Contain($"Disabled {CliDomain.CapabilityId}."));
            Assert.That(api.DisabledId, Is.EqualTo(CliDomain.CapabilityId));
        });
    }

    [Test]
    public void FormatList_saysWhetherTheSoftwareWasFound()
    {
        var listed = CapabilitiesOutputService.FormatList([CliDomain.Capability().Missing("nix").Build()]);

        Assert.That(listed, Does.Contain("available=false"));
    }

    [Test]
    public void FormatList_includesTheReasonWhenCanRunIsFalse()
    {
        var listed = CapabilitiesOutputService.FormatList(
        [
            CliDomain.Capability().UnableToRun("Nix was not found.", "Install Nix: https://nixos.org/download").Build()
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(listed, Does.Contain("canRun=false"));
            Assert.That(listed, Does.Contain("https://nixos.org/download"));
        });
    }

    [Test]
    public void FormatList_omitsReasonsWhenAModuleCanRun()
    {
        Assert.That(
            CapabilitiesOutputService.FormatList([CliDomain.Capability().Enabled().Build()]),
            Does.Contain("canRun=true"));
    }

    [Test]
    public async Task Enable_reportsAParseError()
    {
        var result = await Service(new FakeCapabilityModulesApi()).RunAsync(["enable"]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("enable <id>"));
        });
    }

    [Test]
    public async Task List_reportsAuthenticationFailures()
    {
        var api = new FakeCapabilityModulesApi { ListError = new AuthenticationRequiredException() };

        var result = await Service(api).RunAsync(["list"]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain(AuthenticationRequiredException.LoginHint));
        });
    }

    [Test]
    public async Task List_reportsHttpFailures()
    {
        var api = new FakeCapabilityModulesApi { ListError = new HttpRequestException("connection refused") };

        var result = await Service(api).RunAsync(["list"]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Failed to list capabilities"));
        });
    }

    [Test]
    public async Task Enable_refusesACapabilityWhoseSoftwareIsMissingWithoutCallingTheServer()
    {
        var api = new FakeCapabilityModulesApi { Modules = [CliDomain.Capability().Missing("nix").Build()] };

        var result = await Service(api).RunAsync(["enable", CliDomain.CapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("nix not found"));
            Assert.That(api.EnabledId, Is.Null);
        });
    }

    [Test]
    public async Task Enable_reportsHttpFailures()
    {
        var api = new FakeCapabilityModulesApi { EnableError = new HttpRequestException("connection refused") };

        var result = await Service(api).RunAsync(["enable", CliDomain.CapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Failed to enable"));
        });
    }

    [Test]
    public async Task Disable_reportsHttpFailures()
    {
        var api = new FakeCapabilityModulesApi
        {
            Modules = [CliDomain.Capability().Enabled().Build()],
            DisableError = new HttpRequestException("connection refused")
        };

        var result = await Service(api).RunAsync(["disable", CliDomain.CapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Failed to disable"));
        });
    }

    [Test]
    public async Task Bootstrap_skipsModulesThatAreAlreadyEnabled()
    {
        var api = new FakeCapabilityModulesApi
        {
            Modules =
            [
                CliDomain.Capability().Named("dotnet").Enabled().Build(),
                CliDomain.Capability().Named("docker").Enabled().Build(),
                CliDomain.Capability().Named("codex").Enabled().Build(),
                CliDomain.Capability().Named("cursor").Enabled().Build(),
                CliDomain.Capability().Named("claude").Enabled().Build(),
                CliDomain.Capability().Named("github").Enabled().Build(),
                CliDomain.Capability().Named("forgejo").Enabled().Build()
            ]
        };

        var result = await Service(api).RunAsync(["bootstrap"]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Does.Contain("already enabled"));
            Assert.That(api.EnabledId, Is.Null);
        });
    }

    [Test]
    public void FormatList_explainsAnEmptyRegistry()
    {
        Assert.That(
            CapabilitiesOutputService.FormatList([]),
            Is.EqualTo("The Server reports no capabilities."));
    }

    [Test]
    public async Task Bootstrap_keepsWhatItEnabledWhenALaterEnableFails()
    {
        var api = new FakeCapabilityModulesApi
        {
            Modules = [CliDomain.Capability().Named("dotnet").Build()],
            EnableError = new HttpRequestException("connection refused")
        };

        var result = await Service(api).RunAsync(["bootstrap"]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Failed to enable capabilities"));
        });
    }

    private static CapabilitiesService Service(FakeCapabilityModulesApi api)
        => new(api, new CapabilitiesArgParser());
}
