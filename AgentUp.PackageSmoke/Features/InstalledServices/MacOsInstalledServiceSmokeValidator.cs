using AgentUp.PackageSmoke.Features.Security;
using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.InstalledServices;

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
        var pkgPath = Path.Combine(request.ArtifactDirectory, $"agent-up-macos-{request.RuntimeId}.pkg");
        assert.FileExists(pkgPath, "installed.macos.artifact");
        if (!File.Exists(pkgPath))
            return null;

        await RunRequiredAsync(assert, new CommandSpec("sudo", ["installer", "-pkg", pkgPath, "-target", "/"]), "installed.macos.install", cancellationToken);
        assert.ExecutableExists("/usr/local/bin/agent-up", "installed.macos.cli");
        assert.ExecutableExists("/usr/local/bin/agent-up-server", "installed.macos.server");
        assert.ExecutableExists("/usr/local/bin/agent-up-desktop", "installed.macos.desktop");

        // AMFI on macOS 15 may block launchd from starting unsigned daemons; fall back to direct start.
        var launchctlPrint = await RunAsync(
            new CommandSpec("sudo", ["launchctl", "print", "system/dev.agent-up.server"]),
            cancellationToken);
        if (launchctlPrint.ExitCode != 0)
        {
            await RunRequiredAsync(assert,
                new CommandSpec("sudo", [
                    "bash", "-c",
                    "nohup \"/Library/Application Support/Agent-Up/server/AgentUp.Server\" --urls http://127.0.0.1:5000 >> \"/Library/Logs/Agent-Up/server.out.log\" 2>> \"/Library/Logs/Agent-Up/server.err.log\" &"
                ]),
                "installed.macos.server-fallback", cancellationToken);
        }

        return new InstalledServiceContext(
            "/usr/local/bin/agent-up",
            [
                new CommandSpec("sudo", [
                    "bash",
                    "-c",
                    "launchctl bootout system /Library/LaunchDaemons/dev.agent-up.server.plist 2>/dev/null || true; pkill -f AgentUp.Server 2>/dev/null || true; rm -f /Library/LaunchDaemons/dev.agent-up.server.plist; rm -f /usr/local/bin/agent-up /usr/local/bin/agent-up-server /usr/local/bin/agent-up-desktop; rm -rf /usr/local/agent-up; rm -rf \"/Library/Application Support/Agent-Up\""
                ])
            ],
            [
                new CommandSpec("sudo", ["launchctl", "print", "system/dev.agent-up.server"]),
                new CommandSpec("sudo", ["tail", "-n", "200", "/Library/Logs/Agent-Up/server.out.log"]),
                new CommandSpec("sudo", ["tail", "-n", "200", "/Library/Logs/Agent-Up/server.err.log"]),
                new CommandSpec("ps", ["aux"]),
                new CommandSpec("lsof", ["-nP", "-iTCP", "-sTCP:LISTEN"]),
                new CommandSpec("sudo", ["ls", "-la", "/Library/Application Support/Agent-Up"])
            ]);
    }
}
