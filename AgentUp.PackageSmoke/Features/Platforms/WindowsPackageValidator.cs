using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.Platforms;

public sealed class WindowsPackageValidator : IPackageValidator
{
    private readonly ICommandRunner _commands;

    public WindowsPackageValidator(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<PackageValidationResult> ValidateAsync(PackageValidationRequest request, CancellationToken cancellationToken = default)
    {
        var assert = new FileAssertions();
        var installer = Path.Combine(request.ArtifactDirectory, $"agent-up-windows-{request.RuntimeId}.exe");
        assert.FileExists(installer, "windows.artifact");
        if (!File.Exists(installer))
            return new PackageValidationResult(null, null, assert.Findings);

        var extract = await _commands.RunAsync(new CommandSpec(installer, ["--extract", request.WorkDirectory, "--quiet"]), cancellationToken);
        if (extract.ExitCode != 0)
        {
            assert.Error("windows.extract", $"installer extract failed: {extract.Stderr}{extract.Stdout}");
            return new PackageValidationResult(null, null, assert.Findings);
        }

        var desktop = Path.Combine(request.WorkDirectory, "desktop", "AgentUp.Desktop.exe");
        var server = Path.Combine(request.WorkDirectory, "server", "AgentUp.Server.exe");
        var cli = Path.Combine(request.WorkDirectory, "cli", "AgentUp.CLI.exe");
        assert.FileExists(desktop, "windows.desktop");
        assert.FileExists(server, "windows.server");
        assert.FileExists(cli, "windows.cli");
        assert.Contains(Path.Combine(request.WorkDirectory, "tools", "install-agent-up-server.ps1"), "New-Service", "windows.service");
        assert.Contains(Path.Combine(request.WorkDirectory, "tools", "install-agent-up-server.ps1"), "Start-Service", "windows.service.start");
        assert.Contains(Path.Combine(request.WorkDirectory, "tools", "install-agent-up-server.ps1"), "sc.exe failure", "windows.service.restart");
        assert.Contains(Path.Combine(request.WorkDirectory, "tools", "install-agent-up-server.ps1"), "http://127.0.0.1:5000", "windows.service.url");
        assert.FileExists(Path.Combine(request.WorkDirectory, "tools", "uninstall-agent-up-server.ps1"), "windows.service.uninstall");

        return new PackageValidationResult(server, cli, assert.Findings);
    }
}
