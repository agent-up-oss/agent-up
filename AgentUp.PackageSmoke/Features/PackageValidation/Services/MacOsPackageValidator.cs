using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Services;

public sealed class MacOsPackageValidator : IPackageValidator
{
    private readonly ICommandRunner _commands;

    public MacOsPackageValidator(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<PackageValidationResult> ValidateAsync(PackageValidationRequest request, CancellationToken cancellationToken = default)
    {
        var assert = new FileAssertions();
        var archive = Path.Join(request.ArtifactDirectory, $"agent-up-macos-{request.RuntimeId}.pkg");
        var expanded = Path.Join(request.WorkDirectory, "pkg-expanded");
        assert.FileExists(archive, "macos.artifact");
        if (!File.Exists(archive))
            return new PackageValidationResult(null, null, assert.Findings);

        var expand = await _commands.RunAsync(new CommandSpec("pkgutil", ["--expand-full", archive, expanded]), cancellationToken);
        if (expand.ExitCode != 0)
        {
            assert.Error("macos.expand", $"pkgutil failed: {expand.Stderr}{expand.Stdout}");
            return new PackageValidationResult(null, null, assert.Findings);
        }

        var desktop = FindFirst(expanded, Path.Join("usr", "local", "agent-up", "desktop", "AgentUp.Desktop"));
        var desktopApp = FindFirst(expanded, Path.Join("Applications", "Agent-Up.app", "Contents", "MacOS", "AgentUp.Desktop"));
        var installerApp = FindFirst(expanded, Path.Join("Applications", "Agent-Up Installer.app", "Contents", "MacOS", "AgentUp.InstallerApp"));
        var installerPayloadDesktop = FindFirst(expanded, Path.Join("Applications", "Agent-Up Installer.app", "Contents", "MacOS", "payload", "desktop", "AgentUp.Desktop"));
        var installerPayloadServer = FindFirst(expanded, Path.Join("Applications", "Agent-Up Installer.app", "Contents", "MacOS", "payload", "server", "AgentUp.Server"));
        var installerPayloadCli = FindFirst(expanded, Path.Join("Applications", "Agent-Up Installer.app", "Contents", "MacOS", "payload", "cli", "AgentUp.CLI"));
        var server = FindFirst(expanded, Path.Join("Library", "Application Support", "Agent-Up", "server", "AgentUp.Server"));
        var cli = FindFirst(expanded, Path.Join("usr", "local", "agent-up", "cli", "AgentUp.CLI"));
        var launchd = FindFirst(expanded, Path.Join("Library", "LaunchDaemons", "dev.agent-up.server.plist"));
        var distribution = Directory.EnumerateFiles(expanded, "Distribution", SearchOption.AllDirectories).FirstOrDefault() ?? "";
        var postinstall = FindFirst(expanded, Path.Join("Scripts", "postinstall"));

        assert.ExecutableExists(desktop, "macos.desktop");
        assert.ExecutableExists(desktopApp, "macos.desktop.app");
        assert.ExecutableExists(installerApp, "macos.installer.app");
        assert.ExecutableExists(installerPayloadDesktop, "macos.installer.payload.desktop");
        assert.ExecutableExists(installerPayloadServer, "macos.installer.payload.server");
        assert.ExecutableExists(installerPayloadCli, "macos.installer.payload.cli");
        assert.ExecutableExists(server, "macos.server");
        assert.ExecutableExists(cli, "macos.cli");
        assert.Contains(launchd, "/Library/Application Support/Agent-Up/server/AgentUp.Server", "macos.launchd.server");
        assert.Contains(launchd, "<key>ThrottleInterval</key>", "macos.launchd.throttle");
        assert.Contains(distribution, "DesktopApp.pkg", "macos.distribution.desktop");
        assert.Contains(distribution, "InstallerApp.pkg", "macos.distribution.installer");
        assert.Contains(distribution, "Server.pkg", "macos.distribution.server");
        assert.Contains(distribution, "CLI.pkg", "macos.distribution.cli");
        assert.Contains(postinstall, "launchctl bootstrap system", "macos.postinstall");
        assert.Contains(postinstall, "open -a \"/Applications/Agent-Up Installer.app\"", "macos.postinstall.installer");

        return new PackageValidationResult(server, cli, assert.Findings);
    }

    private static string FindFirst(string root, string suffix)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
               .FirstOrDefault(path => path.EndsWith(suffix, StringComparison.Ordinal)) ?? "";
}
