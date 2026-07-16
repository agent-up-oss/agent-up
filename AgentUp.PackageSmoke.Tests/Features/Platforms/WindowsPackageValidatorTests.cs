using AgentUp.PackageSmoke.Features.Platforms;
using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Tests.Features.Platforms;

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
            Assert.That(command.FileName, Is.EqualTo(installer));
            Assert.That(command.Arguments, Is.EqualTo(new[] { "/layout", Path.Join(workDir, "layout"), "/quiet" }));
            Directory.CreateDirectory(Path.Join(workDir, "layout"));
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
}
