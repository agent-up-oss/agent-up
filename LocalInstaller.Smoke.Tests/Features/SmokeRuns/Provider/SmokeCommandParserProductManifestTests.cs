using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.SmokeRuns.DTOs;
using LocalInstaller.Smoke.Features.SmokeRuns.Interfaces;
using LocalInstaller.Smoke.Features.SmokeRuns.Providers;
using LocalInstaller.Smoke.Features.SmokeRuns.Services;

namespace LocalInstaller.Smoke.Tests.Features.SmokeRuns.Provider;

[TestFixture]
public sealed class SmokeCommandParserProductManifestTests
{
    [Test]
    public void Parse_accepts_product_manifest_forProductAgnosticSmokeRuns()
    {
        var root = TempRoot();
        var manifest = WriteManifest(root, """
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
    public void Parse_usesDefaultProductManifest_whenNoManifestFileIsSupplied()
    {
        var product = new SmokeProductManifest(
            ServiceName: "sample-server",
            CliShimName: "sample",
            ArtifactBaseName: "sample",
            DisplayName: "Sample Product",
            InstallDirName: "Sample Product",
            WorkspaceConfigFileName: "sample.json");

        var result = new SmokeCommandParser(product).Parse(
            ["validate-package", "ubuntu", "linux-x64", "artifacts", "work"]);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Request!.Product.ServiceName, Is.EqualTo("sample-server"));
        Assert.That(result.Request.Product.WorkspaceConfigFileName, Is.EqualTo("sample.json"));
    }

    [Test]
    public void Parse_rejectsMissingMalformedAndUnsafeProductManifests()
    {
        var root = TempRoot();
        var malformed = WriteManifest(root, "{ nope");
        var unsafeManifest = WriteManifest(root, """
            {
              "serviceName": "acme-server",
              "cliShimName": "acme; rm -rf /",
              "artifactBaseName": "acme",
              "displayName": "Acme",
              "installDirName": "Acme",
              "workspaceConfigFileName": "acme.json"
            }
            """, "unsafe-product.json");

        try
        {
            Assert.That(new SmokeCommandParser().Parse(["--product-manifest", Path.Join(root, "missing.json"), "validate-installed-service", "ubuntu", "linux-x64", "artifacts", "work"]).Succeeded, Is.False);
            Assert.That(new SmokeCommandParser().Parse(["--product-manifest", malformed, "validate-installed-service", "ubuntu", "linux-x64", "artifacts", "work"]).Succeeded, Is.False);
            Assert.That(new SmokeCommandParser().Parse(["--product-manifest", unsafeManifest, "validate-installed-service", "ubuntu", "linux-x64", "artifacts", "work"]).Succeeded, Is.False);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ExecuteAsync_forwardsProductManifestToValidationProvider()
    {
        var root = TempRoot();
        var manifest = WriteManifest(root, """
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

    private static string TempRoot()
        => Path.Join(Path.GetTempPath(), "AgentUp-SmokeCommandParser", $"{Guid.NewGuid():N}");

    private static string WriteManifest(string root, string json, string fileName = "sample-product.json")
    {
        Directory.CreateDirectory(root);
        var path = Path.Join(root, fileName);
        File.WriteAllText(path, json);
        return path;
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
