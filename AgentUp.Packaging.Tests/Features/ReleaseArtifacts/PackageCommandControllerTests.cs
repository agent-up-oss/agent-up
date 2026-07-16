using AgentUp.Packaging.Features.MacOsPackages.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Services;
using AgentUp.Packaging.Features.UbuntuPackages.Controllers;
using AgentUp.Packaging.Features.WindowsPackages.Controllers;

namespace AgentUp.Packaging.Tests.Features.ReleaseArtifacts;

[TestFixture]
public class PackageCommandControllerTests
{
    [Test]
    public async Task ExecuteAsync_withUbuntuCommandDispatchesToUbuntuController()
    {
        var calls = new List<(string Target, PackageRequest Request)>();
        var controller = CreateController(
            calls,
            environment: name => name switch
            {
                "CONFIGURATION" => "Debug",
                "AGENTUP_PACKAGE_PAYLOAD_ROOT" => "/payload",
                _ => null,
            });

        var error = new StringWriter();
        var exitCode = await controller.ExecuteAsync(["package", "ubuntu", "linux-x64", "1.2.3"], error);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(error.ToString(), Is.Empty);
        Assert.That(calls, Has.Count.EqualTo(1));
        Assert.That(calls[0].Target, Is.EqualTo("ubuntu"));
        Assert.That(calls[0].Request, Is.EqualTo(new PackageRequest("/repo", "ubuntu", "linux-x64", "1.2.3", "artifacts", "Debug", "/payload")));
    }

    [Test]
    public async Task ExecuteAsync_withPayloadRootArgumentOverridesEnvironment()
    {
        var calls = new List<(string Target, PackageRequest Request)>();
        var controller = CreateController(
            calls,
            environment: name => name == "AGENTUP_PACKAGE_PAYLOAD_ROOT" ? "/env-payload" : null);

        var exitCode = await controller.ExecuteAsync(
            ["package", "windows", "win-x64", "2.0.0", "release-output", "--payload-root", "/arg-payload"],
            new StringWriter());

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(calls, Has.Count.EqualTo(1));
        Assert.That(calls[0].Target, Is.EqualTo("windows"));
        Assert.That(calls[0].Request, Is.EqualTo(new PackageRequest("/repo", "windows", "win-x64", "2.0.0", "release-output", "Release", "/arg-payload")));
    }

    [Test]
    public async Task ExecuteAsync_withMacOsCommandDispatchesToMacOsController()
    {
        var calls = new List<(string Target, PackageRequest Request)>();
        var controller = CreateController(calls);

        var exitCode = await controller.ExecuteAsync(["package", "macos", "osx-arm64", "3.0.0", "dist"], new StringWriter());

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(calls, Has.Count.EqualTo(1));
        Assert.That(calls[0].Target, Is.EqualTo("macos"));
        Assert.That(calls[0].Request, Is.EqualTo(new PackageRequest("/repo", "macos", "osx-arm64", "3.0.0", "dist", "Release")));
    }

    [Test]
    public async Task ExecuteAsync_withUnsupportedPlatformReturnsConfigError()
    {
        var calls = new List<(string Target, PackageRequest Request)>();
        var controller = CreateController(calls);
        var error = new StringWriter();

        var exitCode = await controller.ExecuteAsync(["package", "nixos", "linux-x64", "1.2.3"], error);

        Assert.That(exitCode, Is.EqualTo(78));
        Assert.That(calls, Is.Empty);
        Assert.That(error.ToString(), Does.Contain("Platform 'nixos' is not yet implemented by AgentUp.Packaging."));
    }

    [Test]
    public async Task ExecuteAsync_withInvalidArgsReturnsUsageError()
    {
        var controller = CreateController([]);
        var error = new StringWriter();

        var exitCode = await controller.ExecuteAsync(["package", "ubuntu"], error);

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(error.ToString(), Is.EqualTo("Usage: AgentUp.Packaging package <platform> <runtime-id> <version> [output-dir] [--payload-root <path>]" + Environment.NewLine));
    }

    private static PackageCommandController CreateController(
        List<(string Target, PackageRequest Request)> calls,
        Func<string, string?>? environment = null)
    {
        environment ??= _ => null;

        return new PackageCommandController(new PackageCommandService(
            new FixedRepositoryPathProvider(),
            new DelegateEnvironmentVariableProvider(environment),
            new RecordingUbuntuController(calls),
            new RecordingWindowsController(calls),
            new RecordingMacOsController(calls)));
    }

    private sealed class FixedRepositoryPathProvider : IRepositoryPathProvider
    {
        public string FindRepositoryRoot() => "/repo";
    }

    private sealed class DelegateEnvironmentVariableProvider : IEnvironmentVariableProvider
    {
        private readonly Func<string, string?> _get;

        public DelegateEnvironmentVariableProvider(Func<string, string?> get)
        {
            _get = get;
        }

        public string? Get(string name) => _get(name);
    }

    private sealed class RecordingUbuntuController : IUbuntuPackageController
    {
        private readonly List<(string Target, PackageRequest Request)> _calls;

        public RecordingUbuntuController(List<(string Target, PackageRequest Request)> calls)
        {
            _calls = calls;
        }

        public Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
        {
            _calls.Add(("ubuntu", request));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingWindowsController : IWindowsPackageController
    {
        private readonly List<(string Target, PackageRequest Request)> _calls;

        public RecordingWindowsController(List<(string Target, PackageRequest Request)> calls)
        {
            _calls = calls;
        }

        public Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
        {
            _calls.Add(("windows", request));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMacOsController : IMacOsPackageController
    {
        private readonly List<(string Target, PackageRequest Request)> _calls;

        public RecordingMacOsController(List<(string Target, PackageRequest Request)> calls)
        {
            _calls = calls;
        }

        public Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
        {
            _calls.Add(("macos", request));
            return Task.CompletedTask;
        }
    }
}
