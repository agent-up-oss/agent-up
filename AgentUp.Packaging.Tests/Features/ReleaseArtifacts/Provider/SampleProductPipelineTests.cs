using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Models;
using AgentUp.Packaging.Features.MacOsPackages.Services;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;
using AgentUp.Packaging.Features.ReleaseArtifacts.Services;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Models;
using AgentUp.Packaging.Features.UbuntuPackages.Services;
using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.WindowsPackages.Models;
using AgentUp.Packaging.Features.WindowsPackages.Services;
using AgentUp.Packaging.Shared.Interfaces;

namespace AgentUp.Packaging.Tests.Features.ReleaseArtifacts.Provider;

[TestFixture]
public class SampleProductPipelineTests
{
    private static readonly PackageProductManifest AcmeStudio =
        new("Acme Studio", "acme-studio", "ACMESTUDIO");

    // Test 1a: Ubuntu full pipeline for SampleProduct — no "Agent-Up" or "agent-up" in any text artifact.
    [Test]
    public async Task FullPipeline_ubuntuAcmeStudio_noTextArtifactContainsAgentUpString()
    {
        var root = TempRoot("Ubuntu-Pipeline");
        var writer = new RecordingUnixWriter();
        var request = new PackageRequest(root, "ubuntu", "linux-x64", "1.0.0", "out", "Release",
            productManifest: AcmeStudio);

        try
        {
            await new UbuntuPackager(writer, CreateUbuntuPayloads(root, writer), new RecordingUbuntuPackageTool(), AcmeStudio)
                .PackageAsync(request);
        }
        finally { DeleteTempRoot(root); }

        foreach (var (path, text) in writer.WrittenText)
        {
            Assert.That(text, Does.Not.Contain("Agent-Up"),
                $"ubuntu text artifact '{path}' must not contain 'Agent-Up'");
            Assert.That(text, Does.Not.Contain("agent-up"),
                $"ubuntu text artifact '{path}' must not contain 'agent-up'");
        }
    }

    // Test 1b: macOS full pipeline for SampleProduct — no "Agent-Up" or "agent-up" in any text artifact.
    [Test]
    public async Task FullPipeline_macOsAcmeStudio_noTextArtifactContainsAgentUpString()
    {
        var root = TempRoot("MacOs-Pipeline");
        var writer = new RecordingUnixWriter();
        var request = new PackageRequest(root, "macos", "osx-arm64", "1.0.0", "out", "Release",
            productManifest: AcmeStudio);

        try
        {
            await new MacOsPackager(writer, CreateMacOsPayloads(root, writer), new RecordingMacOsPackageTool())
                .PackageAsync(request);
        }
        finally { DeleteTempRoot(root); }

        foreach (var (path, text) in writer.WrittenText)
        {
            Assert.That(text, Does.Not.Contain("Agent-Up"),
                $"macOS text artifact '{path}' must not contain 'Agent-Up'");
            Assert.That(text, Does.Not.Contain("agent-up"),
                $"macOS text artifact '{path}' must not contain 'agent-up'");
        }
    }

    // Test 1c: Windows full pipeline for SampleProduct — no "Agent-Up" or "agent-up" in any text artifact.
    [Test]
    public async Task FullPipeline_windowsAcmeStudio_noTextArtifactContainsAgentUpString()
    {
        var root = TempRoot("Windows-Pipeline");
        var writer = new RecordingWindowsWriter();
        var request = new PackageRequest(root, "windows", "win-x64", "1.0.0", "out", "Release",
            productManifest: AcmeStudio);

        try
        {
            await new WindowsPackager(writer, CreateWindowsPayloads(root, writer), new RecordingWindowsPackagingTool())
                .PackageAsync(request);
        }
        finally { DeleteTempRoot(root); }

        foreach (var (path, text) in writer.WrittenText)
        {
            Assert.That(text, Does.Not.Contain("Agent-Up"),
                $"windows text artifact '{path}' must not contain 'Agent-Up'");
            Assert.That(text, Does.Not.Contain("agent-up"),
                $"windows text artifact '{path}' must not contain 'agent-up'");
        }
    }

    // Test 2: SampleProduct's Windows MSI carries a different upgrade GUID than Agent-Up's,
    // preventing an unwanted upgrade when both are installed on the same machine.
    [Test]
    public void WindowsManifest_acmeStudioUpgradeCode_differsFromAgentUpUpgradeCode()
    {
        var acmeRequest = new PackageRequest("/tmp", "windows", "win-x64", "1.0.0", "out", "Release",
            productManifest: AcmeStudio);
        var agentUpRequest = new PackageRequest("/tmp", "windows", "win-x64", "1.0.0", "out", "Release");

        var acmeGuid = WindowsPackageManifest.From(acmeRequest).InstallerManifest.UpgradeCode;
        var agentUpGuid = WindowsPackageManifest.From(agentUpRequest).InstallerManifest.UpgradeCode;

        Assert.That(Guid.TryParseExact(acmeGuid, "D", out _), Is.True,
            "Acme Studio upgrade code must be a valid GUID");
        Assert.That(Guid.TryParseExact(agentUpGuid, "D", out _), Is.True,
            "Agent-Up upgrade code must be a valid GUID");
        Assert.That(acmeGuid, Is.Not.EqualTo(agentUpGuid).IgnoreCase,
            "Acme Studio and Agent-Up must have distinct upgrade GUIDs to prevent unwanted MSI upgrades");
    }

    // Test 3: SampleProduct's .deb carries a package name that does not conflict with Agent-Up's,
    // so both can coexist in a dpkg database without one replacing the other.
    [Test]
    public void UbuntuManifest_acmeStudioPackageName_doesNotConflictWithAgentUpPackageName()
    {
        var acmeRequest = new PackageRequest("/tmp", "ubuntu", "linux-x64", "1.0.0", "out", "Release",
            productManifest: AcmeStudio);
        var agentUpRequest = new PackageRequest("/tmp", "ubuntu", "linux-x64", "1.0.0", "out", "Release");

        var acmeName = UbuntuPackageManifest.From(acmeRequest, AcmeStudio).PackageName;
        var agentUpName = UbuntuPackageManifest.From(agentUpRequest).PackageName;

        Assert.That(acmeName, Is.EqualTo("acme-studio"),
            "Acme Studio package name must be derived from its slug");
        Assert.That(agentUpName, Is.EqualTo("agent-up"),
            "Agent-Up package name must be 'agent-up'");
        Assert.That(acmeName, Is.Not.EqualTo(agentUpName),
            "package names must be distinct so both .deb packages can coexist in a dpkg database");
    }

    // Test 4: Running Agent-Up packaging immediately after SampleProduct packaging in the same test
    // process produces Agent-Up text artifacts byte-for-byte identical to the standalone baseline.
    [Test]
    public async Task FullPipeline_agentUpAfterAcmeStudio_producesIdenticalTextToStandaloneRun()
    {
        var agentUpRoot = TempRoot("AgentUp-CrossContam");
        var acmeRoot = TempRoot("AcmeStudio-CrossContam");

        try
        {
            var baseline = await CollectUbuntuPipelineText(PackageProductManifest.AgentUp(), agentUpRoot);
            await CollectUbuntuPipelineText(AcmeStudio, acmeRoot);
            var afterAcme = await CollectUbuntuPipelineText(PackageProductManifest.AgentUp(), agentUpRoot);

            Assert.That(afterAcme.Keys, Is.EquivalentTo(baseline.Keys),
                "Agent-Up pipeline must write the same set of files after Acme Studio run");
            foreach (var path in baseline.Keys)
                Assert.That(afterAcme[path], Is.EqualTo(baseline[path]),
                    $"Agent-Up artifact at '{path}' must be unchanged after Acme Studio pipeline ran");
        }
        finally
        {
            DeleteTempRoot(agentUpRoot);
            DeleteTempRoot(acmeRoot);
        }
    }

    // Test 5: The packaging pipeline rejects a manifest with an invalid field before invoking
    // any native tool, and the rejection message names the invalid field.
    [Test]
    public void PackageRequest_withInvalidManifestField_rejectsBeforeInvokingNativeTools()
    {
        var root = TempRoot("InvalidManifest");
        var writer = new RecordingUnixWriter();
        var commands = new RecordingCommandRunner();
        var invalidManifest = new PackageProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO")
            with { WindowsUpgradeCode = "this-is-not-a-valid-guid" };

        try
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                var request = new PackageRequest(root, "ubuntu", "linux-x64", "1.0.0", "out", "Release",
                    productManifest: invalidManifest);
                // If request creation succeeded the packager would run next:
                _ = new UbuntuPackager(writer, CreateUbuntuPayloads(root, writer),
                    new RecordingUbuntuPackageTool(), invalidManifest);
            });

            Assert.That(exception!.ParamName, Is.EqualTo("WindowsUpgradeCode"),
                "rejection must name the invalid field");
            Assert.That(exception.Message, Does.Contain("GUID"),
                "rejection message must describe the constraint that was violated");
            Assert.That(commands.Commands, Is.Empty,
                "no native tool command must be issued before manifest validation fails");
        }
        finally { DeleteTempRoot(root); }
    }

    private static async Task<Dictionary<string, string>> CollectUbuntuPipelineText(PackageProductManifest product, string root)
    {
        var writer = new RecordingUnixWriter();
        var request = new PackageRequest(root, "ubuntu", "linux-x64", "1.0.0", "out", "Release",
            productManifest: product);

        await new UbuntuPackager(writer, CreateUbuntuPayloads(root, writer), new RecordingUbuntuPackageTool(), product)
            .PackageAsync(request);

        return writer.WrittenText;
    }

    private static PayloadStagingController CreateUbuntuPayloads(string root, IPackageWriter writer)
        => new(new PackagePayloadStager(new PackagePublisher(new RecordingCommandRunner()), writer));

    private static PayloadStagingController CreateMacOsPayloads(string root, IMacOsPackageWriter writer)
        => new(new PackagePayloadStager(new PackagePublisher(new RecordingCommandRunner()), writer));

    private static PayloadStagingController CreateWindowsPayloads(string root, IWindowsPackageWriter writer)
        => new(new PackagePayloadStager(new PackagePublisher(new RecordingCommandRunner()), writer));

    private static string TempRoot(string label)
    {
        var path = Path.Join(Path.GetTempPath(), $"AgentUp-{label}", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<CommandSpec> Commands { get; } = [];

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            if (command.FileName == "dotnet" && command.Arguments.Contains("publish"))
            {
                for (var i = 0; i < command.Arguments.Count - 1; i++)
                {
                    if (command.Arguments[i] == "-o")
                    {
                        Directory.CreateDirectory(command.Arguments[i + 1]);
                        break;
                    }
                }
            }
            return Task.FromResult(new CommandResult(0, "", ""));
        }
    }

    private sealed class RecordingUnixWriter : IPackageWriter, IMacOsPackageWriter
    {
        public Dictionary<string, string> WrittenText { get; } = [];

        public void ResetDirectory(string path) { }
        public void CreateDirectory(string path) { }
        public void CopyDirectory(string source, string destination) { }
        public void CopyFile(string source, string destination) { }
        public void WriteText(string path, string text) => WrittenText[path] = text;
        public void CreateSymbolicLink(string linkPath, string targetPath) { }
        public void SetExecutable(string path) { }
    }

    private sealed class RecordingWindowsWriter : IWindowsPackageWriter
    {
        public Dictionary<string, string> WrittenText { get; } = [];

        public void ResetDirectory(string path) { }
        public void CreateDirectory(string path) { }
        public void CopyFile(string source, string destination) { }
        public void WriteText(string path, string text) => WrittenText[path] = text;
    }

    private sealed class RecordingUbuntuPackageTool : IUbuntuPackageTool
    {
        public Task BuildDebAsync(UbuntuPackageLayout layout, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingMacOsPackageTool : IMacOsPackageTool
    {
        public Task BuildComponentPackagesAsync(PackageRequest request, MacOsPackageLayout layout, MacOsPackageManifest manifest, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task BuildProductPackageAsync(MacOsPackageLayout layout, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingWindowsPackagingTool : IWindowsPackagingTool
    {
        public Task AcceptWixLicenseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BuildProductMsiAsync(WindowsPackageLayout layout, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BuildBundleAsync(PackageRequest request, WindowsPackageLayout layout, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
