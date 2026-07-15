using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.Platforms;

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
        var archive = Path.Combine(request.ArtifactDirectory, $"agent-up-macos-{request.RuntimeId}.pkg");
        var expanded = Path.Combine(request.WorkDirectory, "pkg-expanded");
        assert.FileExists(archive, "macos.artifact");
        if (!File.Exists(archive))
            return new PackageValidationResult(null, null, assert.Findings);

        var expand = await _commands.RunAsync(new CommandSpec("pkgutil", ["--expand-full", archive, expanded]), cancellationToken);
        if (expand.ExitCode != 0)
        {
            assert.Error("macos.expand", $"pkgutil failed: {expand.Stderr}{expand.Stdout}");
            return new PackageValidationResult(null, null, assert.Findings);
        }

        var desktop = FindFirst(expanded, Path.Combine("Applications", "Agent-Up.app", "Contents", "MacOS", "AgentUp.Desktop"));
        var server = FindFirst(expanded, Path.Combine("Library", "Application Support", "Agent-Up", "server", "AgentUp.Server"));
        var cli = FindFirst(expanded, Path.Combine("usr", "local", "agent-up", "cli", "AgentUp.CLI"));
        var desktopPlist = FindFirst(expanded, Path.Combine("Applications", "Agent-Up.app", "Contents", "Info.plist"));
        var launchd = FindFirst(expanded, Path.Combine("Library", "LaunchDaemons", "dev.agent-up.server.plist"));
        var distribution = Directory.EnumerateFiles(expanded, "Distribution", SearchOption.AllDirectories).FirstOrDefault() ?? "";
        var postinstall = FindFirst(expanded, Path.Combine("Scripts", "postinstall"));

        assert.ExecutableExists(desktop, "macos.desktop");
        assert.ExecutableExists(server, "macos.server");
        assert.ExecutableExists(cli, "macos.cli");
        assert.FileExists(desktopPlist, "macos.desktop.plist");
        assert.Contains(launchd, "/Library/Application Support/Agent-Up/server/AgentUp.Server", "macos.launchd.server");
        assert.Contains(launchd, "<key>ThrottleInterval</key>", "macos.launchd.throttle");
        assert.Contains(distribution, "DesktopApp.pkg", "macos.distribution.desktop");
        assert.Contains(distribution, "Server.pkg", "macos.distribution.server");
        assert.Contains(distribution, "CLI.pkg", "macos.distribution.cli");
        assert.Contains(postinstall, "launchctl bootstrap system", "macos.postinstall");

        return new PackageValidationResult(server, cli, assert.Findings);
    }

    private static string FindFirst(string root, string suffix)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
               .FirstOrDefault(path => path.EndsWith(suffix, StringComparison.Ordinal)) ?? "";
}
