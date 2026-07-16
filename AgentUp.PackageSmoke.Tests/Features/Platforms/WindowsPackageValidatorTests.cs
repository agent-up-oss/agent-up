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
            Assert.That(command.Arguments, Is.EqualTo(new[] { "/layout", Path.Combine(workDir, "layout"), "/quiet" }));
            CreateLayout(Path.Combine(workDir, "layout"));
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

    private static void CreateLayout(string root)
    {
        WriteText(Path.Combine(root, "AttachedContainer", "Product.msi"), "");
    }

    private static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
