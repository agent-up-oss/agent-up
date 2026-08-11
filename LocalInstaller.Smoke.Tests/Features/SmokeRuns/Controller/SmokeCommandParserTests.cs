using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.SmokeRuns.DTOs;
using LocalInstaller.Smoke.Features.SmokeRuns.Interfaces;
using LocalInstaller.Smoke.Features.SmokeRuns.Providers;
using LocalInstaller.Smoke.Features.SmokeRuns.Services;

namespace LocalInstaller.Smoke.Tests.Features.SmokeRuns.Controller;

[TestFixture]
public sealed class SmokeCommandParserTests
{
    [Test]
    public void Parse_accepts_package_validation_command()
    {
        var result = new SmokeCommandParser().Parse(["validate-package", "ubuntu", "linux-x64", "artifacts", "work"]);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Request!.Command, Is.EqualTo("validate-package"));
        Assert.That(result.Request.Platform, Is.EqualTo("ubuntu"));
        Assert.That(result.Request.RuntimeId, Is.EqualTo("linux-x64"));
        Assert.That(Path.IsPathFullyQualified(result.Request.ArtifactDirectory), Is.True);
        Assert.That(Path.IsPathFullyQualified(result.Request.WorkDirectory), Is.True);
    }

    [Test]
    public void Parse_accepts_installer_flow_command_with_payload_root()
    {
        var result = new SmokeCommandParser().Parse(["validate-installer-flow", "ubuntu", "work", "payload"]);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Request!.Command, Is.EqualTo("validate-installer-flow"));
        Assert.That(result.Request.RuntimeId, Is.Empty);
        Assert.That(result.Request.ArtifactDirectory, Is.Empty);
        Assert.That(Path.IsPathFullyQualified(result.Request.PayloadRoot!), Is.True);
    }

    [Test]
    public async Task ExecuteAsync_helpPrintsProductAgnosticInterface()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new SmokeCommandService(
                new CapturingValidationProvider(),
                new NoOpWorkDirectoryProvider(),
                new SmokeCommandParser())
            .ExecuteAsync(["--help"], output, error);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(error.ToString(), Is.Empty);
        Assert.That(output.ToString(), Does.Contain("--product-manifest <path>"));
        Assert.That(output.ToString(), Does.Contain("validate-installed-service"));
        Assert.That(output.ToString(), Does.Contain("serviceName"));
    }

    [Test]
    public void Parse_rejects_unknown_command()
    {
        var result = new SmokeCommandParser().Parse(["unknown"]);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Usage, Does.Contain("AgentUp.PackageSmoke"));
    }

    private sealed class CapturingValidationProvider : ISmokeValidationProvider
    {
        public SmokeCommandRequest? Request { get; private set; }

        public Task<SmokeCommandResult> ValidatePackageAsync(SmokeCommandRequest request, CancellationToken cancellationToken = default)
            => CaptureAsync(request);

        public Task<SmokeCommandResult> ValidateInstallerFlowAsync(SmokeCommandRequest request, CancellationToken cancellationToken = default)
            => CaptureAsync(request);

        public Task<SmokeCommandResult> ValidateInstalledServiceAsync(SmokeCommandRequest request, CancellationToken cancellationToken = default)
            => CaptureAsync(request);

        private Task<SmokeCommandResult> CaptureAsync(SmokeCommandRequest request)
        {
            Request = request;
            return Task.FromResult(new SmokeCommandResult(true, []));
        }
    }

    private sealed class NoOpWorkDirectoryProvider : ISmokeWorkDirectoryProvider
    {
        public void Prepare(string workDirectory) { }

        public Task WritePackageEnvironmentAsync(
            string workDirectory,
            PackageValidationResult result,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
