using LocalInstaller.Smoke.Features.InstalledServiceValidation.DTOs;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Services;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;
using LocalInstaller.Smoke.Tests.Features.InstalledServiceValidation.Fake;
using LocalInstaller.Smoke.Tests.Features.PackageValidation.Fake;
using LocalInstaller.Smoke.Tests.Features.RuntimeSecurity.Fake;

namespace LocalInstaller.Smoke.Tests.Features.InstalledServiceValidation.Provider;

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
                CreateWindowsPackageInstall(DefaultInstallDirectory());
            if (IsWindowsInstallCoreCommand(command))
                CreateWindowsCoreInstall(DefaultInstallDirectory());
            if (IsInstalledCliCommand(command, "start"))
                return new CommandResult(0, "Started workspace \"Installed Service Smoke Workspace\"", "");
            if (IsInstalledCliCommand(command, "status"))
                return new CommandResult(0, "Name:       Installed Service Smoke Workspace\nState:      Running\n", "");
            return new CommandResult(0, "", "");
        });

        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL", "1");
            using var validator = new WindowsInstalledServiceSmokeValidator(commands, probe, new NullRuntimeSecurityChecks(),
                new HttpClient(new FakeTraySessionHttpHandler()));
            var result = await validator.ValidateAsync(new InstalledServiceSmokeRequest("windows", "win-x64", artifactDir, workDir, ProductConfig: AgentUpProduct()));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerUrl, Is.EqualTo("http://127.0.0.1:5000"));
            Assert.That(commands.Commands.Any(command => command.FileName == "msiexec.exe" && command.Arguments.Contains("/l*vx!", StringComparer.OrdinalIgnoreCase)), Is.True);
            Assert.That(commands.Commands.Any(IsWindowsInstallCoreCommand), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "sc.exe" && command.Arguments.SequenceEqual(["start", "agent-up-server"])), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "sc.exe" && command.Arguments.SequenceEqual(["failure", "agent-up-server", "reset=", "86400", "actions=", "restart/5000/restart/5000/restart/5000"])), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "sc.exe" && command.Arguments.SequenceEqual(["failureflag", "agent-up-server", "1"])), Is.True);
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
            Assert.That(commands.Commands.Any(command =>
                    command.FileName == "powershell.exe" &&
                    command.Arguments.Last().Contains("HKLM:", StringComparison.Ordinal) &&
                    command.Arguments.Last().Contains("AGENTUP_TRAY_AUTOSTART_NAME", StringComparison.Ordinal) &&
                    command.Arguments.Last().Contains("AGENTUP_TRAY_AUTOSTART_VALUE", StringComparison.Ordinal) &&
                    command.Environment is not null &&
                    command.Environment.TryGetValue("AGENTUP_TRAY_AUTOSTART_NAME", out var trayName) &&
                    trayName == "Agent-Up" &&
                    command.Environment.TryGetValue("AGENTUP_TRAY_AUTOSTART_VALUE", out var trayValue) &&
                    trayValue == $"\"{Path.Join(DefaultInstallDirectory(), "tray", "AgentUp.Tray.exe")}\""),
                Is.True);
            Assert.That(probe.Calls, Has.Count.EqualTo(2)); // initial ready check + post-restart ready check
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
                CreateWindowsPackageInstall(DefaultInstallDirectory());
            if (IsWindowsInstallCoreCommand(command))
                CreateWindowsCoreInstall(DefaultInstallDirectory());
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new WindowsInstalledServiceSmokeValidator(commands, new FakeServerProbe(null), new NullRuntimeSecurityChecks())
                .ValidateAsync(new InstalledServiceSmokeRequest("windows", "win-x64", artifactDir, workDir, ProductConfig: AgentUpProduct()));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Findings.Any(finding => finding.Code == "installed.server.ready"), Is.True);
            Assert.That(commands.Commands.Any(command => command.FileName == "msiexec.exe" && command.Arguments.Contains("/l*vx!", StringComparer.OrdinalIgnoreCase)), Is.True);
            Assert.That(commands.Commands.Any(IsWindowsInstallCoreCommand), Is.True);
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
                .ValidateAsync(new InstalledServiceSmokeRequest("windows", "win-x64", artifactDir, workDir, ProductConfig: AgentUpProduct()));

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

    private static void CreateWindowsPackageInstall(string installDir)
    {
        WriteText(Path.Join(installDir, "installer", "AgentUp.InstallerApp.exe"), "");
        Directory.CreateDirectory(Path.Join(installDir, "installer", "payload"));
    }

    private static void CreateWindowsCoreInstall(string installDir)
    {
        WriteText(Path.Join(installDir, "bin", "agent-up.cmd"), "");
        WriteText(Path.Join(installDir, "cli", "AgentUp.CLI.exe"), "");
        WriteText(Path.Join(installDir, "tray", "AgentUp.Tray.exe"), "");
    }

    private static InstalledServiceProductManifest AgentUpProduct()
        => new(
            ServiceName: "agent-up-server",
            CliShimName: "agent-up",
            ArtifactBaseName: "agent-up",
            DisplayName: "Agent-Up",
            InstallDirName: "Agent-Up",
            InstallerExecutableName: "AgentUp.InstallerApp",
            DesktopExecutableName: "AgentUp.Desktop",
            ServerExecutableName: "AgentUp.Server",
            CliExecutableName: "AgentUp.CLI",
            TrayExecutableName: "AgentUp.Tray");

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

    private static bool IsWindowsInstallCoreCommand(CommandSpec command)
        => command.FileName == Path.Join(DefaultInstallDirectory(), "installer", "AgentUp.InstallerApp.exe")
           && command.Arguments.SequenceEqual([
               "--payload-root",
               Path.Join(DefaultInstallDirectory(), "installer", "payload"),
               "--install-core"
           ]);
}
