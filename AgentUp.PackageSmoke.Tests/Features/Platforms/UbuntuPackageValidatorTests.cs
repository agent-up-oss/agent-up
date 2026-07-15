using AgentUp.PackageSmoke.Features.Platforms;
using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Tests.Features.Platforms;

[TestFixture]
public class UbuntuPackageValidatorTests
{
    [Test]
    public async Task ValidateAsync_reportsExpectedPathsAndContractFindings()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("The Ubuntu package adapter verifies Unix symlinks.");

        var root = Path.Combine(Path.GetTempPath(), "AgentUp-Smoke-Ubuntu", Guid.NewGuid().ToString());
        var artifactDir = Path.Combine(root, "artifacts");
        var workDir = Path.Combine(root, "work");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Combine(artifactDir, "agent-up-ubuntu-linux-x64.deb"), "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (command.Arguments.Contains("-x"))
                CreateUbuntuRoot(Path.Combine(workDir, "root"));
            if (command.Arguments.Contains("-e"))
                CreateUbuntuControl(Path.Combine(workDir, "control"));
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new UbuntuPackageValidator(commands).ValidateAsync(new PackageValidationRequest("ubuntu", "linux-x64", artifactDir, workDir));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerPath, Is.EqualTo(Path.Combine(workDir, "root", "opt", "agent-up", "server", "AgentUp.Server")));
            Assert.That(result.CliPath, Is.EqualTo(Path.Combine(workDir, "root", "opt", "agent-up", "cli", "AgentUp.CLI")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateUbuntuRoot(string root)
    {
        WriteExecutable(Path.Combine(root, "opt", "agent-up", "desktop", "AgentUp.Desktop"));
        WriteExecutable(Path.Combine(root, "opt", "agent-up", "server", "AgentUp.Server"));
        WriteExecutable(Path.Combine(root, "opt", "agent-up", "cli", "AgentUp.CLI"));
        Directory.CreateDirectory(Path.Combine(root, "usr", "bin"));
        File.CreateSymbolicLink(Path.Combine(root, "usr", "bin", "agent-up"), "/opt/agent-up/cli/AgentUp.CLI");
        WriteText(Path.Combine(root, "usr", "share", "applications", "agent-up.desktop"), "Exec=/opt/agent-up/desktop/AgentUp.Desktop\nIcon=agent-up\n");
        WriteText(Path.Combine(root, "usr", "share", "pixmaps", "agent-up.png"), "png");
        WriteText(Path.Combine(root, "etc", "systemd", "system", "agent-up-server.service"), "ExecStart=/opt/agent-up/server/AgentUp.Server\nEnvironment=DOTNET_BUNDLE_EXTRACT_BASE_DIR=/var/cache/agent-up\nCacheDirectory=agent-up\nRestartSec=5\n");
    }

    private static void CreateUbuntuControl(string control)
    {
        WriteText(Path.Combine(control, "postinst"), "systemctl enable --now agent-up-server.service\n");
        WriteText(Path.Combine(control, "prerm"), "systemctl stop agent-up-server.service\n");
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
