using AgentUp.PackageSmoke.Features.Security;
using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.InstalledServices;

public sealed class WindowsInstalledServiceSmokeValidator : InstalledServiceSmokeValidator
{
    public WindowsInstalledServiceSmokeValidator(ICommandRunner commands, IServerProbe serverProbe, IRuntimeSecurityChecks securityChecks)
        : base(commands, serverProbe, securityChecks)
    {
    }

    protected override async Task<InstalledServiceContext?> InstallAsync(
        InstalledServiceSmokeRequest request,
        FileAssertions assert,
        CancellationToken cancellationToken)
    {
        var installer = Path.Combine(request.ArtifactDirectory, $"agent-up-windows-{request.RuntimeId}.exe");
        var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Agent-Up");
        assert.FileExists(installer, "installed.windows.artifact");
        if (!File.Exists(installer))
            return null;

        await RunRequiredAsync(assert, new CommandSpec(installer, ["/quiet", "/norestart"]), "installed.windows.install", cancellationToken);

        var cli = Path.Combine(installDir, "cli", "AgentUp.CLI.exe");
        assert.FileExists(Path.Combine(installDir, "bin", "agent-up.cmd"), "installed.windows.path.shim");
        assert.FileExists(cli, "installed.windows.cli");

        var pathCheck = """
            $installDir = [System.IO.Path]::GetFullPath($env:AGENTUP_INSTALL_DIR);
            $key = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Agent-Up';
            if (-not (Test-Path $key)) { throw 'Agent-Up uninstall registration missing' }
            $path = [Environment]::GetEnvironmentVariable('Path', 'Machine');
            $bin = [System.IO.Path]::GetFullPath((Join-Path $installDir 'bin')).TrimEnd('\');
            $entries = ($path -split ';' | Where-Object { $_ } | ForEach-Object { [System.IO.Path]::GetFullPath($_).TrimEnd('\') });
            if (-not ($entries | Where-Object { [string]::Equals($_, $bin, [System.StringComparison]::OrdinalIgnoreCase) })) { throw "Agent-Up PATH entry missing: $bin" }
            """;

        await RunRequiredAsync(
            assert,
            new CommandSpec("powershell.exe", ["-NoProfile", "-Command", pathCheck], Environment: new Dictionary<string, string> { ["AGENTUP_INSTALL_DIR"] = installDir }),
            "installed.windows.registration",
            cancellationToken);

        return new InstalledServiceContext(
            cli,
            [new CommandSpec(installer, ["/uninstall", "/quiet", "/norestart"])],
            [new CommandSpec("powershell.exe", ["-NoProfile", "-Command", "Get-Service agent-up-server -ErrorAction SilentlyContinue | Format-List *"])]);
    }
}
