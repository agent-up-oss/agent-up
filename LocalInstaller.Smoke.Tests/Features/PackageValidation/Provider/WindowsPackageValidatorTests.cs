using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;
using LocalInstaller.Smoke.Features.PackageValidation.Providers;
using LocalInstaller.Smoke.Features.PackageValidation.Services;
using LocalInstaller.Smoke.Tests.Features.PackageValidation.Fake;

namespace LocalInstaller.Smoke.Tests.Features.PackageValidation.Provider;

[TestFixture]
public class WindowsPackageValidatorTests
{
    [Test]
    public async Task ValidateAsync_reportsExpectedPathsAndContractFindings()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-Smoke-Windows", Guid.NewGuid().ToString());
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        Directory.CreateDirectory(artifactDir);
        var installer = Path.Join(artifactDir, "agent-up-windows-win-x64.exe");
        File.WriteAllText(installer, "");
        File.WriteAllText(Path.Join(artifactDir, "agent-up-windows-win-x64.msi"), "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            Assert.That(command.FileName, Is.EqualTo("powershell.exe"));
            Assert.That(command.Arguments, Is.EqualTo(new[] { "-NoProfile", "-Command", "$process = Start-Process -FilePath $env:AGENTUP_SMOKE_INSTALLER -ArgumentList @('/layout', $env:AGENTUP_SMOKE_LAYOUT, '/quiet') -Wait -PassThru; exit $process.ExitCode" }));
            Assert.That(command.Environment, Is.Not.Null);
            Assert.That(command.Environment!["AGENTUP_SMOKE_INSTALLER"], Is.EqualTo(installer));
            Assert.That(command.Environment["AGENTUP_SMOKE_LAYOUT"], Is.EqualTo(Path.Join(workDir, "layout")));
            Directory.CreateDirectory(Path.Join(workDir, "layout"));
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new WindowsPackageValidator(new WindowsPackageArchiveProvider(commands)).ValidateAsync(new PackageValidationRequest("windows", "win-x64", artifactDir, workDir, AgentUpProduct()));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerPath, Is.Null);
            Assert.That(result.CliPath, Is.Null);
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

}
