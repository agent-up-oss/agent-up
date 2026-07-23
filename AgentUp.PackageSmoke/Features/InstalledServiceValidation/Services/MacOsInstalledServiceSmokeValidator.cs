using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Shared.Providers;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;

public sealed class MacOsInstalledServiceSmokeValidator : InstalledServiceSmokeValidator
{
    public MacOsInstalledServiceSmokeValidator(ICommandRunner commands, IServerProbe serverProbe, IRuntimeSecurityChecks securityChecks)
        : base(commands, serverProbe, securityChecks)
    {
    }

    protected override async Task<InstalledServiceContext?> InstallAsync(
        InstalledServiceSmokeRequest request,
        FileAssertions assert,
        CancellationToken cancellationToken)
    {
        var product = request.Product;
        var pkgPath = Path.Join(request.ArtifactDirectory, $"{product.ArtifactBaseName}-macos-{request.RuntimeId}.pkg");
        assert.FileExists(pkgPath, "installed.macos.artifact");
        if (!File.Exists(pkgPath))
            return null;

        await RunRequiredAsync(assert, new CommandSpec("sudo", ["installer", "-pkg", pkgPath, "-target", "/"]), "installed.macos.install", cancellationToken);
        assert.ExecutableExists($"/usr/local/bin/{product.CliShimName}", "installed.macos.cli");
        assert.ExecutableExists($"/usr/local/bin/{product.ServiceName}", "installed.macos.server");
        assert.ExecutableExists($"/usr/local/bin/{product.CliShimName}-desktop", "installed.macos.desktop");

        // AMFI on macOS 15 may block launchd from starting unsigned daemons; fall back to direct start.
        var launchctlPrint = await RunAsync(
            new CommandSpec("sudo", ["launchctl", "print", $"system/dev.{product.CliShimName}.server"]),
            cancellationToken);
        if (launchctlPrint.ExitCode != 0)
        {
            await RunRequiredAsync(assert,
                new CommandSpec("sudo", [
                    "bash", "-c",
                    $"nohup \"/Library/Application Support/{product.InstallDirName}/server/AgentUp.Server\" --urls http://127.0.0.1:5000 >> \"/Library/Logs/{product.InstallDirName}/server.out.log\" 2>> \"/Library/Logs/{product.InstallDirName}/server.err.log\" &"
                ]),
                "installed.macos.server-fallback", cancellationToken);
        }

        return new InstalledServiceContext(
            product.CliShimName,
            null,
            [
                new CommandSpec("sudo", [
                    "bash",
                    "-c",
                    $"launchctl bootout system /Library/LaunchDaemons/dev.{product.CliShimName}.server.plist 2>/dev/null || true; pkill -f AgentUp.Server 2>/dev/null || true; rm -f /Library/LaunchDaemons/dev.{product.CliShimName}.server.plist; rm -f /usr/local/bin/{product.CliShimName} /usr/local/bin/{product.ServiceName} /usr/local/bin/{product.CliShimName}-desktop; rm -rf /usr/local/{product.CliShimName}; rm -rf \"/Library/Application Support/{product.InstallDirName}\"; rm -rf \"/Applications/{product.InstallDirName}.app\" \"/Applications/{product.InstallDirName} Installer.app\""
                ])
            ],
            [
                new CommandSpec("sudo", ["launchctl", "print", $"system/dev.{product.CliShimName}.server"]),
                new CommandSpec("sudo", ["tail", "-n", "200", $"/Library/Logs/{product.InstallDirName}/server.out.log"]),
                new CommandSpec("sudo", ["tail", "-n", "200", $"/Library/Logs/{product.InstallDirName}/server.err.log"]),
                new CommandSpec("ps", ["aux"]),
                new CommandSpec("lsof", ["-nP", "-iTCP", "-sTCP:LISTEN"]),
                new CommandSpec("sudo", ["ls", "-la", $"/Library/Application Support/{product.InstallDirName}"])
            ]);
    }
}
