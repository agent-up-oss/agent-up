using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;
using AgentUp.PackageSmoke.Features.SmokeRuns.Providers;
using AgentUp.PackageSmoke.Features.SmokeRuns.Services;

namespace AgentUp.PackageSmoke.Tests.Features.SmokeRuns.Controller;

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
    public void Parse_accepts_product_manifest_forProductAgnosticSmokeRuns()
    {
        var root = TempRoot();
        var manifest = Path.Join(root, "sample-product.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(manifest, """
            {
              "serviceName": "acme-server",
              "cliShimName": "acme",
              "artifactBaseName": "acme",
              "displayName": "Acme",
              "installDirName": "Acme",
              "workspaceConfigFileName": "acme.json"
            }
            """);

        try
        {
            var result = new SmokeCommandParser().Parse(
                ["--product-manifest", manifest, "validate-installed-service", "ubuntu", "linux-x64", "artifacts", "work"]);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Request!.Product.ServiceName, Is.EqualTo("acme-server"));
            Assert.That(result.Request.Product.CliShimName, Is.EqualTo("acme"));
            Assert.That(result.Request.Product.WorkspaceConfigFileName, Is.EqualTo("acme.json"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
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
    public async Task ExecuteAsync_forwardsProductManifestToValidationProvider()
    {
        var root = TempRoot();
        var manifest = Path.Join(root, "sample-product.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(manifest, """
            {
              "serviceName": "acme-server",
              "cliShimName": "acme",
              "artifactBaseName": "acme",
              "displayName": "Acme",
              "installDirName": "Acme",
              "workspaceConfigFileName": "acme.json"
            }
            """);
        var validation = new CapturingValidationProvider();

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = await new SmokeCommandService(
                    validation,
                    new NoOpWorkDirectoryProvider(),
                    new SmokeCommandParser())
                .ExecuteAsync(
                    ["validate-installed-service", "ubuntu", "linux-x64", "artifacts", "work", "--product-manifest", manifest],
                    output,
                    error);

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(validation.Request!.Product.ServiceName, Is.EqualTo("acme-server"));
            Assert.That(validation.Request.Product.CliShimName, Is.EqualTo("acme"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Parse_rejects_unknown_command()
    {
        var result = new SmokeCommandParser().Parse(["unknown"]);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Usage, Does.Contain("AgentUp.PackageSmoke"));
    }

    private static string TempRoot()
        => Path.Join(Path.GetTempPath(), "AgentUp-SmokeCommandParser", $"{Guid.NewGuid():N}");

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
