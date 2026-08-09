using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.WindowsInstallation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using AgentUp.Installers.Features.WindowsInstallation.Providers;

namespace AgentUp.Installers.Tests.Features.WindowsInstallation.Provider;

[TestFixture]
public sealed class WindowsInstallerPlanTests
{
    [Test]
    public void PlanInstall_withNonAgentUpManifest_exposesCustomProductServiceAndCliNames()
    {
        var adapter = new WindowsInstallerPlatformAdapter(
            new RecordingCommandRunner(),
            new RecordingWindowsFileSystem(),
            AcmeStudioOptions(),
            new RequiredCommandRunner(new RecordingCommandRunner()),
            new DockerPrerequisite(new DockerPrerequisiteProvider(new RecordingCommandRunner()), new Version(27, 0, 0)));

        var plan = adapter.PlanInstall(AcmeStudioSession());
        var titles = string.Join(" ", plan.Select(operation => operation.Title));

        Assert.Multiple(() =>
        {
            Assert.That(titles, Does.Contain("Acme Studio"));
            Assert.That(titles, Does.Contain("acme-studio-server"));
            Assert.That(titles, Does.Contain("acme-studio CLI"));
            Assert.That(titles, Does.Not.Contain("Agent-Up"));
            Assert.That(titles, Does.Not.Contain("agent-up"));
        });
    }

    private static ProductManifest AcmeStudio()
        => new("Acme Studio", "acme-studio", "ACMESTUDIO");

    private static InstallerSession AcmeStudioSession()
        => InstallerSession.CreateDefault(
                AcmeStudio(),
                new Version(1, 2, 3),
                @"C:\Program Files\Acme Studio",
                PayloadSelection.Bundled("Acme Studio", new Version(1, 2, 3)))
            with { ServerUrl = "http://127.0.0.1:5001" };

    private static WindowsInstallerOptions AcmeStudioOptions()
        => new(
            new WindowsInstallPayload("/payload/desktop", "/payload/server", "/payload/cli", "/payload/tray"),
            new WindowsInstallerPaths(
                RootDirectory: @"C:\Program Files\Acme Studio",
                DesktopDirectory: @"C:\Program Files\Acme Studio\desktop",
                ServerDirectory: @"C:\Program Files\Acme Studio\server",
                CliDirectory: @"C:\Program Files\Acme Studio\cli",
                TrayDirectory: @"C:\Program Files\Acme Studio\tray",
                BinDirectory: @"C:\Program Files\Acme Studio\bin",
                StartMenuShortcutPath: @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Acme Studio\Acme Studio.lnk",
                CliShimName: "acme-studio.cmd",
                UninstallScriptName: "uninstall-acme-studio.ps1"));

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProcessResult(0, "", ""));
    }

    private sealed class RecordingWindowsFileSystem : IWindowsInstallerFileSystem
    {
        public void ResetDirectory(string path) { }

        public void CopyDirectory(string sourceDirectory, string destinationDirectory) { }

        public void WriteText(string path, string contents) { }

        public bool FileExists(string path) => false;

        public void DeleteFile(string path) { }

        public void CreateDirectory(string path) { }

        public void DeleteDirectory(string path) { }
    }
}
