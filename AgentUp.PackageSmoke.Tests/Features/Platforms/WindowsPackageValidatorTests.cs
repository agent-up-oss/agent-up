using AgentUp.PackageSmoke.Features.Platforms;
using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Tests.Features.Platforms;

[TestFixture]
public class WindowsPackageValidatorTests
{
    [Test]
    public async Task ValidateAsync_reportsExpectedPathsAndContractFindings()
    {
        var root = Path.Combine(Path.GetTempPath(), "AgentUp-Smoke-Windows", Guid.NewGuid().ToString());
        var artifactDir = Path.Combine(root, "artifacts");
        var workDir = Path.Combine(root, "work");
        Directory.CreateDirectory(artifactDir);
        var installer = Path.Combine(artifactDir, "agent-up-windows-win-x64.exe");
        File.WriteAllText(installer, "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            Assert.That(command.FileName, Is.EqualTo(installer));
            Assert.That(command.Arguments, Is.EqualTo(new[] { "--extract", workDir, "--quiet" }));
            CreateExtractedInstaller(workDir);
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new WindowsPackageValidator(commands).ValidateAsync(new PackageValidationRequest("windows", "win-x64", artifactDir, workDir));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerPath, Is.EqualTo(Path.Combine(workDir, "server", "AgentUp.Server.exe")));
            Assert.That(result.CliPath, Is.EqualTo(Path.Combine(workDir, "cli", "AgentUp.CLI.exe")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateExtractedInstaller(string root)
    {
        WriteText(Path.Combine(root, "desktop", "AgentUp.Desktop.exe"), "");
        WriteText(Path.Combine(root, "server", "AgentUp.Server.exe"), "");
        WriteText(Path.Combine(root, "cli", "AgentUp.CLI.exe"), "");
        WriteText(Path.Combine(root, "tools", "install-agent-up-server.ps1"), "New-Service\nStart-Service\nsc.exe failure\nhttp://127.0.0.1:5000\n");
        WriteText(Path.Combine(root, "tools", "uninstall-agent-up-server.ps1"), "Delete service\n");
    }

    private static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
