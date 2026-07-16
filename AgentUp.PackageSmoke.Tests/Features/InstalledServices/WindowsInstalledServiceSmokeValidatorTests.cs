using AgentUp.PackageSmoke.Features.InstalledServices;
using AgentUp.PackageSmoke.Features.Validation;
using AgentUp.PackageSmoke.Tests.Features.Platforms;
using AgentUp.PackageSmoke.Tests.Features.Security;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServices;

[TestFixture]
public class WindowsInstalledServiceSmokeValidatorTests
{
    [Test]
    public async Task ValidateAsync_installsValidatesCliAndAlwaysUninstalls()
    {
        var root = Path.Combine(Path.GetTempPath(), "AgentUp-InstalledSmoke-Windows", Guid.NewGuid().ToString());
        var artifactDir = Path.Combine(root, "artifacts");
        var workDir = Path.Combine(root, "work");
        Directory.CreateDirectory(artifactDir);
        var installer = Path.Combine(artifactDir, "agent-up-windows-win-x64.exe");
        File.WriteAllText(installer, "");
        var probe = new FakeServerProbe("http://127.0.0.1:5000");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (command.FileName == installer && command.Arguments.Contains("/quiet") && !command.Arguments.Contains("/uninstall"))
                CreateWindowsInstall(DefaultInstallDirectory());
            if (command.FileName.EndsWith("AgentUp.CLI.exe", StringComparison.Ordinal) && command.Arguments.SequenceEqual(["start"]))
                return new CommandResult(0, "Started workspace \"Installed Service Smoke Workspace\"", "");
            if (command.FileName.EndsWith("AgentUp.CLI.exe", StringComparison.Ordinal) && command.Arguments.SequenceEqual(["status"]))
                return new CommandResult(0, "Name:       Installed Service Smoke Workspace\nState:      Running\n", "");
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new WindowsInstalledServiceSmokeValidator(commands, probe, new NullRuntimeSecurityChecks())
                .ValidateAsync(new InstalledServiceSmokeRequest("windows", "win-x64", artifactDir, workDir));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerUrl, Is.EqualTo("http://127.0.0.1:5000"));
            Assert.That(commands.Commands.Any(command => command.FileName == installer && command.Arguments.Contains("/uninstall")), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName.EndsWith("AgentUp.CLI.exe", StringComparison.Ordinal) && command.Arguments.SequenceEqual(["start"])), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName.EndsWith("AgentUp.CLI.exe", StringComparison.Ordinal) && command.Arguments.SequenceEqual(["status"])), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "powershell.exe" && command.Arguments.Last().Contains("DisplayName -eq 'Agent-Up'", StringComparison.Ordinal)), Is.True);
            Assert.That(probe.Calls, Has.Count.EqualTo(1));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ValidateAsync_runsDiagnosticsAndUninstallsWhenServiceNeverBecomesReady()
    {
        var root = Path.Combine(Path.GetTempPath(), "AgentUp-InstalledSmoke-Windows", Guid.NewGuid().ToString());
        var artifactDir = Path.Combine(root, "artifacts");
        var workDir = Path.Combine(root, "work");
        Directory.CreateDirectory(artifactDir);
        var installer = Path.Combine(artifactDir, "agent-up-windows-win-x64.exe");
        File.WriteAllText(installer, "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (command.FileName == installer && !command.Arguments.Contains("/uninstall"))
                CreateWindowsInstall(DefaultInstallDirectory());
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new WindowsInstalledServiceSmokeValidator(commands, new FakeServerProbe(null), new NullRuntimeSecurityChecks())
                .ValidateAsync(new InstalledServiceSmokeRequest("windows", "win-x64", artifactDir, workDir));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Findings.Any(finding => finding.Code == "installed.server.ready"), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "powershell.exe" && command.Arguments.Last().Contains("Get-Service", StringComparison.Ordinal)), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == installer && command.Arguments.Contains("/uninstall")), Is.True);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateWindowsInstall(string installDir)
    {
        WriteText(Path.Combine(installDir, "bin", "agent-up.cmd"), "");
        WriteText(Path.Combine(installDir, "cli", "AgentUp.CLI.exe"), "");
    }

    private static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string DefaultInstallDirectory()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(programFiles, "Agent-Up");
    }
}
