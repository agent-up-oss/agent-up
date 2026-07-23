using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Shared.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;
using AgentUp.PackageSmoke.Tests.Features.PackageValidation.Fake;

namespace AgentUp.PackageSmoke.Tests.Features.PackageValidation.Provider;

[TestFixture]
public class UbuntuPackageValidatorTests
{
    [Test]
    public async Task ValidateAsync_reportsExpectedPathsAndContractFindings()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("The Ubuntu package adapter verifies Unix symlinks.");

        var root = Path.Join(Path.GetTempPath(), "AgentUp-Smoke-Ubuntu", Guid.NewGuid().ToString());
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "agent-up-ubuntu-linux-x64.deb"), "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (command.Arguments.Contains("-x"))
                CreateUbuntuRoot(Path.Join(workDir, "root"));
            if (command.Arguments.Contains("-e"))
                CreateUbuntuControl(Path.Join(workDir, "control"));
            return new CommandResult(0, "", "");
        });

        try
        {
            var result = await new UbuntuPackageValidator(new UbuntuPackageArchiveProvider(commands)).ValidateAsync(new PackageValidationRequest("ubuntu", "linux-x64", artifactDir, workDir));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerPath, Is.EqualTo(Path.Join(workDir, "root", "opt", "agent-up", "server", "AgentUp.Server")));
            Assert.That(result.CliPath, Is.EqualTo(Path.Join(workDir, "root", "opt", "agent-up", "cli", "AgentUp.CLI")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateUbuntuRoot(string root)
    {
        WriteExecutable(Path.Join(root, "opt", "agent-up", "desktop", "AgentUp.Desktop"));
        WriteExecutable(Path.Join(root, "opt", "agent-up", "server", "AgentUp.Server"));
        WriteExecutable(Path.Join(root, "opt", "agent-up", "cli", "AgentUp.CLI"));
        Directory.CreateDirectory(Path.Join(root, "usr", "bin"));
        File.CreateSymbolicLink(Path.Join(root, "usr", "bin", "agent-up"), "/opt/agent-up/cli/AgentUp.CLI");
        WriteText(Path.Join(root, "usr", "share", "applications", "agent-up.desktop"), "Exec=/opt/agent-up/desktop/AgentUp.Desktop\nIcon=agent-up\n");
        WriteText(Path.Join(root, "usr", "share", "pixmaps", "agent-up.png"), "png");
        WriteText(Path.Join(root, "etc", "systemd", "system", "agent-up-server.service"), "ExecStart=/opt/agent-up/server/AgentUp.Server\nEnvironment=DOTNET_BUNDLE_EXTRACT_BASE_DIR=/var/cache/agent-up\nCacheDirectory=agent-up\nRestartSec=5\n");
    }

    private static void CreateUbuntuControl(string control)
    {
        WriteText(Path.Join(control, "postinst"), "systemctl enable --now agent-up-server.service\n");
        WriteText(Path.Join(control, "prerm"), "systemctl stop agent-up-server.service\n");
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
