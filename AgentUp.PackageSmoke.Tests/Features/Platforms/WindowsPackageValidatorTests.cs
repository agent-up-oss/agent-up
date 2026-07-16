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
        File.WriteAllText(Path.Combine(artifactDir, "agent-up-windows-win-x64.msi"), "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            Assert.That(command.FileName, Is.EqualTo(installer));
            Assert.That(command.Arguments, Is.EqualTo(new[] { "/layout", Path.Combine(workDir, "layout"), "/quiet" }));
            WriteFile(Path.Combine(workDir, "layout", "AgentUp.InstallerApp.exe"));
            WriteFile(Path.Combine(workDir, "layout", "payload", "desktop", "AgentUp.Desktop.exe"));
            WriteFile(Path.Combine(workDir, "layout", "payload", "server", "AgentUp.Server.exe"));
            WriteFile(Path.Combine(workDir, "layout", "payload", "cli", "AgentUp.CLI.exe"));
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new WindowsPackageValidator(commands).ValidateAsync(new PackageValidationRequest("windows", "win-x64", artifactDir, workDir));

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

    private static void WriteFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
    }
}
