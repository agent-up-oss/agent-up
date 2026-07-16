using AgentUp.PackageSmoke.Features.Security;
using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.InstalledServices;

public abstract class InstalledServiceSmokeValidator : IInstalledServiceSmokeValidator
{
    private readonly ICommandRunner _commands;
    private readonly IServerProbe _serverProbe;
    private readonly IRuntimeSecurityChecks _securityChecks;

    protected InstalledServiceSmokeValidator(ICommandRunner commands, IServerProbe serverProbe, IRuntimeSecurityChecks securityChecks)
    {
        _commands = commands;
        _serverProbe = serverProbe;
        _securityChecks = securityChecks;
    }

    public async Task<InstalledServiceSmokeResult> ValidateAsync(InstalledServiceSmokeRequest request, CancellationToken cancellationToken = default)
    {
        var assert = new FileAssertions();
        var context = await InstallAsync(request, assert, cancellationToken);

        try
        {
            if (context is null || assert.Findings.Any(finding => finding.Severity == FindingSeverity.Error))
                return new InstalledServiceSmokeResult(null, assert.Findings);

            await RunRequiredAsync(assert, new CommandSpec(context.CliPath, ["--version"]), "installed.cli.version", cancellationToken);
            var readyUrl = await _serverProbe.WaitForReadyAsync(
                request.PrimaryServerUrl,
                request.FallbackServerUrl,
                Path.Combine(request.WorkDirectory, "service-workspaces-before.json"),
                cancellationToken);

            if (readyUrl is null)
            {
                assert.Error("installed.server.ready", $"Installed service did not become ready at {request.PrimaryServerUrl} or {request.FallbackServerUrl}.");
                await RunDiagnosticsAsync(context, cancellationToken);
                return new InstalledServiceSmokeResult(null, assert.Findings);
            }

            await _securityChecks.RunAsync(readyUrl, assert, cancellationToken);
            await SmokeCliWorkspaceAsync(request, context.CliPath, readyUrl, assert, cancellationToken);
            return new InstalledServiceSmokeResult(readyUrl, assert.Findings);
        }
        finally
        {
            if (context is not null)
                await UninstallAsync(context, cancellationToken);
        }
    }

    protected abstract Task<InstalledServiceContext?> InstallAsync(
        InstalledServiceSmokeRequest request,
        FileAssertions assert,
        CancellationToken cancellationToken);

    protected async Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken)
        => await _commands.RunAsync(command, cancellationToken);

    protected async Task RunRequiredAsync(FileAssertions assert, CommandSpec command, string code, CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
            assert.Error(code, $"{command.FileName} failed: {result.Stderr}{result.Stdout}");
    }

    private async Task SmokeCliWorkspaceAsync(
        InstalledServiceSmokeRequest request,
        string cliPath,
        string serverUrl,
        FileAssertions assert,
        CancellationToken cancellationToken)
    {
        var repo = Path.Combine(request.WorkDirectory, "example-workspace");
        Directory.CreateDirectory(repo);
        await File.WriteAllTextAsync(Path.Combine(repo, "agent-up.json"), """
            {
              "name": "Installed Service Smoke Workspace",
              "applications": []
            }
            """, cancellationToken);

        await RunRequiredAsync(assert, new CommandSpec("git", ["init", "-q"], repo), "installed.git.init", cancellationToken);
        await RunRequiredAsync(assert, new CommandSpec("git", ["config", "user.email", "ci@agent-up.local"], repo), "installed.git.email", cancellationToken);
        await RunRequiredAsync(assert, new CommandSpec("git", ["config", "user.name", "Agent-Up CI"], repo), "installed.git.name", cancellationToken);
        await RunRequiredAsync(assert, new CommandSpec("git", ["add", "agent-up.json"], repo), "installed.git.add", cancellationToken);
        await RunRequiredAsync(assert, new CommandSpec("git", ["commit", "-q", "-m", "Add service smoke workspace"], repo), "installed.git.commit", cancellationToken);

        var environment = new Dictionary<string, string> { ["AGENTUP_SERVER_URL"] = serverUrl };
        var start = await _commands.RunAsync(new CommandSpec(cliPath, ["start"], repo, environment), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(request.WorkDirectory, "cli-start.log"), start.Stdout + start.Stderr, cancellationToken);
        if (start.ExitCode != 0 || !start.Stdout.Contains("Started workspace \"Installed Service Smoke Workspace\"", StringComparison.Ordinal))
            assert.Error("installed.cli.start", $"CLI start failed or returned unexpected output: {start.Stderr}{start.Stdout}");

        var status = await _commands.RunAsync(new CommandSpec(cliPath, ["status"], repo, environment), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(request.WorkDirectory, "cli-status.log"), status.Stdout + status.Stderr, cancellationToken);
        if (status.ExitCode != 0
            || !status.Stdout.Contains("Name:       Installed Service Smoke Workspace", StringComparison.Ordinal)
            || !status.Stdout.Contains("State:      Running", StringComparison.Ordinal))
            assert.Error("installed.cli.status", $"CLI status failed or returned unexpected output: {status.Stderr}{status.Stdout}");
    }

    private async Task RunDiagnosticsAsync(InstalledServiceContext context, CancellationToken cancellationToken)
    {
        foreach (var command in context.DiagnosticCommands)
            await _commands.RunAsync(command, cancellationToken);
    }

    private async Task UninstallAsync(InstalledServiceContext context, CancellationToken cancellationToken)
    {
        foreach (var command in context.UninstallCommands)
            await _commands.RunAsync(command, cancellationToken);
    }
}
