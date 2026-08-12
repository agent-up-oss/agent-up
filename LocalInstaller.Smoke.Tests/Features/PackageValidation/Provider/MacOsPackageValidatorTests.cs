using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;
using LocalInstaller.Smoke.Features.PackageValidation.Providers;
using LocalInstaller.Smoke.Features.PackageValidation.Services;
using LocalInstaller.Smoke.Tests.Features.PackageValidation.Fake;

namespace LocalInstaller.Smoke.Tests.Features.PackageValidation.Provider;

[TestFixture]
public class MacOsPackageValidatorTests
{
    [Test]
    public async Task ValidateAsync_reportsExpectedPathsAndContractFindings()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-Smoke-MacOs", Guid.NewGuid().ToString());
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "agent-up-macos-osx-arm64.pkg"), "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            Assert.That(command.FileName, Is.EqualTo("pkgutil"));
            Assert.That(command.Arguments, Does.Contain("--expand-full"));
            CreateExpandedPackage(Path.Join(workDir, "pkg-expanded"));
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new MacOsPackageValidator(new MacOsPackageArchiveProvider(commands)).ValidateAsync(new PackageValidationRequest("macos", "osx-arm64", artifactDir, workDir, AgentUpProduct()));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerPath, Does.EndWith(Path.Join("Agent-Up Installer.app", "Contents", "MacOS", "payload", "server", "AgentUp.Server")));
            Assert.That(result.CliPath, Does.EndWith(Path.Join("Agent-Up Installer.app", "Contents", "MacOS", "payload", "cli", "AgentUp.CLI")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static PackageProductManifest AgentUpProduct()
        => new(
            "agent-up-server",
            "agent-up",
            "agent-up",
            "Agent-Up",
            "Agent-Up",
            InstallerExecutableName: "AgentUp.InstallerApp",
            DesktopExecutableName: "AgentUp.Desktop",
            ServerExecutableName: "AgentUp.Server",
            CliExecutableName: "AgentUp.CLI",
            TrayExecutableName: "AgentUp.Tray");

    private static void CreateExpandedPackage(string root)
    {
        WriteExecutable(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Agent-Up Installer.app", "Contents", "MacOS", "AgentUp.InstallerApp"));
        WriteText(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Agent-Up Installer.app", "Contents", "Info.plist"), "CFBundleIconFile\nAgent-Up.png\n");
        WriteText(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Agent-Up Installer.app", "Contents", "Resources", "Agent-Up.png"), "");
        WriteExecutable(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Agent-Up Installer.app", "Contents", "MacOS", "payload", "desktop", "AgentUp.Desktop"));
        WriteExecutable(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Agent-Up Installer.app", "Contents", "MacOS", "payload", "server", "AgentUp.Server"));
        WriteExecutable(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Agent-Up Installer.app", "Contents", "MacOS", "payload", "cli", "AgentUp.CLI"));
        WriteText(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Agent-Up Installer.app", "Contents", "MacOS", "payload", "icon", "Agent-Up.png"), "");
        WriteText(Path.Join(root, "InstallerApp.pkg", "Scripts", "postinstall"), "open -a \"/Applications/Agent-Up Installer.app\"\n");
        WriteText(Path.Join(root, "Distribution"), "InstallerApp.pkg\n");
    }

    private static void WriteExecutable(string path)
    {
        WriteText(path, "");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
