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

        var result = await Service(api).RunAsync(["disable", CliDomain.UnknownCapabilityId], "/repo");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("not in the local registry"));
            Assert.That(api.DisabledId, Is.Null);
        });
    }

    [Test]
    public async Task Disable_disablesAnEnabledModule()
    {
        var api = new FakeCapabilityModulesApi { Modules = [CliDomain.Capability().Enabled().Build()] };

        var result = await Service(api).RunAsync(["disable", CliDomain.CapabilityId], "/repo");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Does.Contain($"Disabled {CliDomain.CapabilityId}."));
            Assert.That(api.DisabledId, Is.EqualTo(CliDomain.CapabilityId));
        });
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
        var result = await Service(new FakeCapabilityModulesApi()).RunAsync(["enable"], "/repo");

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

        var result = await Service(api).RunAsync(["list"], "/repo");

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

        var result = await Service(api).RunAsync(["list"], "/repo");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Failed to list capabilities"));
        });
    }

    [Test]
    public async Task Enable_with_version_enables_that_package_when_another_version_is_already_enabled()
    {
        var api = new FakeCapabilityModulesApi
        {
            Modules =
            [
                CliDomain.Capability().Versioned("1.0.0").Enabled().Build(),
                CliDomain.Capability().Versioned("2.0.0").Build()
            ]
        };

        var result = await Service(api).RunAsync(
            ["enable", CliDomain.CapabilityId, "--version", "2.0.0"],
            "/repo");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Does.Contain("Enabled"));
            Assert.That(result.Message, Does.Not.Contain("already enabled"));
            Assert.That(api.EnabledId, Is.EqualTo(CliDomain.CapabilityId));
            Assert.That(api.EnabledVersion, Is.EqualTo("2.0.0"));
        });
    }

    [Test]
    public async Task Enable_with_version_rejects_a_version_that_is_not_listed()
    {
        var api = new FakeCapabilityModulesApi
        {
            Modules = [CliDomain.Capability().Versioned("1.0.0").Enabled().Build()]
        };

        var result = await Service(api).RunAsync(
            ["enable", CliDomain.CapabilityId, "--version", "9.9.9"],
            "/repo");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("version '9.9.9'"));
            Assert.That(result.Message, Does.Contain("not in the local registry"));
            Assert.That(api.EnabledId, Is.Null);
        });
    }

    [Test]
    public async Task Enable_reportsHttpFailures()
    {
        var api = new FakeCapabilityModulesApi { EnableError = new HttpRequestException("connection refused") };

        var result = await Service(api).RunAsync(["enable", CliDomain.CapabilityId], "/repo");

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

        var result = await Service(api).RunAsync(["disable", CliDomain.CapabilityId], "/repo");

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
                CliDomain.Capability().Named("github").Enabled().Build()
            ]
        };

        var result = await Service(api).RunAsync(["bootstrap"], "/repo");

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
            Is.EqualTo("No capability modules in the Server registry."));
    }

    [Test]
    public async Task Bootstrap_reportsAPackFailureWithoutEnabling()
    {
        var api = new FakeCapabilityModulesApi();
        var packer = new FakeCapabilityPacker
        {
            Succeeded = false,
            Message = "Error: Packing first-party capabilities failed."
        };

        var result = await new CapabilitiesService(api, packer, new CapabilitiesArgParser())
            .RunAsync(["bootstrap"], "/repo");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Packing first-party capabilities failed"));
            Assert.That(api.EnabledId, Is.Null);
        });
    }

    private static CapabilitiesService Service(FakeCapabilityModulesApi api)
        => new(api, new FakeCapabilityPacker(), new CapabilitiesArgParser());
}
