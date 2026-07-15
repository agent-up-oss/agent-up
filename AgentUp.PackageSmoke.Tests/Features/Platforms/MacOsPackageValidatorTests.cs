using AgentUp.PackageSmoke.Features.Platforms;
using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Tests.Features.Platforms;

[TestFixture]
public class MacOsPackageValidatorTests
{
    [Test]
    public async Task ValidateAsync_reportsExpectedPathsAndContractFindings()
    {
        var root = Path.Combine(Path.GetTempPath(), "AgentUp-Smoke-MacOs", Guid.NewGuid().ToString());
        var artifactDir = Path.Combine(root, "artifacts");
        var workDir = Path.Combine(root, "work");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Combine(artifactDir, "agent-up-macos-osx-arm64.pkg"), "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            Assert.That(command.FileName, Is.EqualTo("pkgutil"));
            Assert.That(command.Arguments, Does.Contain("--expand-full"));
            CreateExpandedPackage(Path.Combine(workDir, "pkg-expanded"));
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new MacOsPackageValidator(commands).ValidateAsync(new PackageValidationRequest("macos", "osx-arm64", artifactDir, workDir));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerPath, Does.EndWith(Path.Combine("Library", "Application Support", "Agent-Up", "server", "AgentUp.Server")));
            Assert.That(result.CliPath, Does.EndWith(Path.Combine("usr", "local", "agent-up", "cli", "AgentUp.CLI")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateExpandedPackage(string root)
    {
        WriteExecutable(Path.Combine(root, "DesktopApp.pkg", "Payload", "Applications", "Agent-Up.app", "Contents", "MacOS", "AgentUp.Desktop"));
        WriteText(Path.Combine(root, "DesktopApp.pkg", "Payload", "Applications", "Agent-Up.app", "Contents", "Info.plist"), "<plist />");
        WriteExecutable(Path.Combine(root, "Server.pkg", "Payload", "Library", "Application Support", "Agent-Up", "server", "AgentUp.Server"));
        WriteText(Path.Combine(root, "Server.pkg", "Payload", "Library", "LaunchDaemons", "dev.agent-up.server.plist"), "/Library/Application Support/Agent-Up/server/AgentUp.Server\n<key>ThrottleInterval</key>\n");
        WriteText(Path.Combine(root, "Server.pkg", "Scripts", "postinstall"), "launchctl bootstrap system\n");
        WriteExecutable(Path.Combine(root, "CLI.pkg", "Payload", "usr", "local", "agent-up", "cli", "AgentUp.CLI"));
        WriteText(Path.Combine(root, "Distribution"), "DesktopApp.pkg\nServer.pkg\nCLI.pkg\n");
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
