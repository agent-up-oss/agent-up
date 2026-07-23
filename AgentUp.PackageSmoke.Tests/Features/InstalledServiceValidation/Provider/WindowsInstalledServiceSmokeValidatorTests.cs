using AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation.Fake;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Tests.Features.PackageValidation.Fake;
using AgentUp.PackageSmoke.Tests.Features.RuntimeSecurity.Fake;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation.Provider;

[TestFixture]
public class WindowsInstalledServiceSmokeValidatorTests
{
    [Test]
    public async Task ValidateAsync_installsValidatesCliAndAlwaysUninstalls()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstalledSmoke-Windows", Guid.NewGuid().ToString());
        var previousSkip = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        Directory.CreateDirectory(artifactDir);
        var installer = Path.Join(artifactDir, "agent-up-windows-win-x64.exe");
        var productMsi = Path.Join(artifactDir, "agent-up-windows-win-x64.msi");
        File.WriteAllText(installer, "");
        File.WriteAllText(productMsi, "");
        var probe = new FakeServerProbe("http://127.0.0.1:5000");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (command.FileName == "msiexec.exe" && command.Arguments.Take(4).SequenceEqual(["/i", productMsi, "/qn", "/norestart"]))
                CreateWindowsInstall(DefaultInstallDirectory());
            if (IsInstalledCliCommand(command, "start"))
                return new CommandResult(0, "Started workspace \"Installed Service Smoke Workspace\"", "");
            if (IsInstalledCliCommand(command, "status"))
                return new CommandResult(0, "Name:       Installed Service Smoke Workspace\nState:      Running\n", "");
            return new CommandResult(0, "", "");
        });

        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL", "1");
            var result = await new WindowsInstalledServiceSmokeValidator(commands, probe, new NullRuntimeSecurityChecks())
                .ValidateAsync(new InstalledServiceSmokeRequest("windows", "win-x64", artifactDir, workDir));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerUrl, Is.EqualTo("http://127.0.0.1:5000"));
            Assert.That(commands.Commands.Any(command => command.FileName == "msiexec.exe" && command.Arguments.Contains("/l*vx!", StringComparer.OrdinalIgnoreCase)), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "sc.exe" && command.Arguments.SequenceEqual(["start", "agent-up-server"])), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "msiexec.exe" && command.Arguments.Take(4).SequenceEqual(["/x", productMsi, "/qn", "/norestart"])), Is.True);
            Assert.That(commands.Commands.Any(command => IsInstalledCliCommand(command, "start")), Is.True);
            Assert.That(commands.Commands.Any(command => IsInstalledCliCommand(command, "status")), Is.True);
            Assert.That(commands.Commands.Any(command =>
                    command.FileName == "powershell.exe" &&
                    command.Arguments.Last().Contains("DisplayName -eq $displayName", StringComparison.Ordinal) &&
                    command.Environment is not null &&
                    command.Environment.TryGetValue("AGENTUP_PRODUCT_DISPLAY_NAME", out var displayName) &&
                    displayName == "Agent-Up"),
                Is.True);
            Assert.That(probe.Calls, Has.Count.EqualTo(1));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL", previousSkip);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ValidateAsync_runsDiagnosticsAndUninstallsWhenServiceNeverBecomesReady()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstalledSmoke-Windows", Guid.NewGuid().ToString());
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        Directory.CreateDirectory(artifactDir);
        var installer = Path.Join(artifactDir, "agent-up-windows-win-x64.exe");
        var productMsi = Path.Join(artifactDir, "agent-up-windows-win-x64.msi");
        File.WriteAllText(installer, "");
        File.WriteAllText(productMsi, "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (command.FileName == "msiexec.exe" && command.Arguments.Take(4).SequenceEqual(["/i", productMsi, "/qn", "/norestart"]))
                CreateWindowsInstall(DefaultInstallDirectory());
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new WindowsInstalledServiceSmokeValidator(commands, new FakeServerProbe(null), new NullRuntimeSecurityChecks())
                .ValidateAsync(new InstalledServiceSmokeRequest("windows", "win-x64", artifactDir, workDir));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Findings.Any(finding => finding.Code == "installed.server.ready"), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "msiexec.exe" && command.Arguments.Contains("/l*vx!", StringComparer.OrdinalIgnoreCase)), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "sc.exe" && command.Arguments.SequenceEqual(["start", "agent-up-server"])), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "powershell.exe" && command.Arguments.Last().Contains("Get-Service", StringComparison.Ordinal)), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "msiexec.exe" && command.Arguments.Take(4).SequenceEqual(["/x", productMsi, "/qn", "/norestart"])), Is.True);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ValidateAsync_reportsMsiFailureWindowFromAggressiveLog()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstalledSmoke-Windows", Guid.NewGuid().ToString());
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        Directory.CreateDirectory(artifactDir);
        var installer = Path.Join(artifactDir, "agent-up-windows-win-x64.exe");
        var productMsi = Path.Join(artifactDir, "agent-up-windows-win-x64.msi");
        File.WriteAllText(installer, "");
        File.WriteAllText(productMsi, "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (command.FileName == "msiexec.exe" && command.Arguments.Take(4).SequenceEqual(["/i", productMsi, "/qn", "/norestart"]))
            {
                var logPath = command.Arguments.Last();
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.WriteAllText(logPath, string.Join(Environment.NewLine, Enumerable.Range(1, 70).Select(index => $"context {index}").Concat([
                    "ActionStart: InstallFiles",
                    "MSI (s) (00:00): Product: Agent-Up -- Error 1939. Service install failed.",
                    "Action ended: InstallFiles. Return value 3.",
                    "trailing detail"
                ])));
                return new CommandResult(1603, "", "");
            }

            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new WindowsInstalledServiceSmokeValidator(commands, new FakeServerProbe(null), new NullRuntimeSecurityChecks())
                .ValidateAsync(new InstalledServiceSmokeRequest("windows", "win-x64", artifactDir, workDir));

            var finding = result.Findings.Single(finding => finding.Code == "installed.windows.install");
            Assert.That(finding.Message, Does.Contain("Error 1939"));
            Assert.That(finding.Message, Does.Contain("Return value 3"));
            Assert.That(finding.Message, Does.Not.Contain("context 1"));
            Assert.That(commands.Commands.Any(command => command.FileName == "msiexec.exe" && command.Arguments.Contains("/l*vx!", StringComparer.OrdinalIgnoreCase)), Is.True);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateWindowsInstall(string installDir)
    {
        WriteText(Path.Join(installDir, "bin", "agent-up.cmd"), "");
        WriteText(Path.Join(installDir, "cli", "AgentUp.CLI.exe"), "");
    }

    private static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string DefaultInstallDirectory()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Join(programFiles, "Agent-Up");
    }

    private static bool IsInstalledCliCommand(CommandSpec command, string argument)
        => command.FileName == "powershell.exe"
           && command.Arguments.SequenceEqual(["-NoProfile", "-Command", $"Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; agent-up.cmd {argument}"])
           && command.Environment is not null
           && command.Environment.TryGetValue("PATH", out var path)
           && path.Split(Path.PathSeparator).Contains(Path.Join(DefaultInstallDirectory(), "bin"))
           && command.Environment.TryGetValue("AGENTUP_SMOKE_WORKING_DIRECTORY", out var workingDirectory)
           && workingDirectory.EndsWith(Path.Join("work", "example-workspace"), StringComparison.Ordinal);
}
