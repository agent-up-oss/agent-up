using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.PackageValidation;

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
            var result = await new MacOsPackageValidator(commands).ValidateAsync(new PackageValidationRequest("macos", "osx-arm64", artifactDir, workDir));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerPath, Does.EndWith(Path.Join("Library", "Application Support", "Agent-Up", "server", "AgentUp.Server")));
            Assert.That(result.CliPath, Does.EndWith(Path.Join("usr", "local", "agent-up", "cli", "AgentUp.CLI")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateExpandedPackage(string root)
    {
        WriteExecutable(Path.Join(root, "DesktopApp.pkg", "Payload", "usr", "local", "agent-up", "desktop", "AgentUp.Desktop"));
        WriteExecutable(Path.Join(root, "Server.pkg", "Payload", "Library", "Application Support", "Agent-Up", "server", "AgentUp.Server"));
        WriteText(Path.Join(root, "Server.pkg", "Payload", "Library", "LaunchDaemons", "dev.agent-up.server.plist"), "/Library/Application Support/Agent-Up/server/AgentUp.Server\n<key>ThrottleInterval</key>\n");
        WriteText(Path.Join(root, "Server.pkg", "Scripts", "postinstall"), "launchctl bootstrap system\n");
        WriteExecutable(Path.Join(root, "CLI.pkg", "Payload", "usr", "local", "agent-up", "cli", "AgentUp.CLI"));
        WriteText(Path.Join(root, "Distribution"), "DesktopApp.pkg\nServer.pkg\nCLI.pkg\n");
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
