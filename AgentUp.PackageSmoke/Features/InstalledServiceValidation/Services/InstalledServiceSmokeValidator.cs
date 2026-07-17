using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;

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

            await RunRequiredAsync(assert, CliCommand(context, "--version"), "installed.cli.version", cancellationToken);
            var readyUrl = await _serverProbe.WaitForReadyAsync(
                request.PrimaryServerUrl,
                request.FallbackServerUrl,
                Path.Join(request.WorkDirectory, "service-workspaces-before.json"),
                cancellationToken);

            if (readyUrl is null)
            {
                assert.Error("installed.server.ready", $"Installed service did not become ready at {request.PrimaryServerUrl} or {request.FallbackServerUrl}.");
                await RunDiagnosticsAsync(context, cancellationToken);
                return new InstalledServiceSmokeResult(null, assert.Findings);
            }

            await _securityChecks.RunAsync(readyUrl, assert, cancellationToken);
            await SmokeCliWorkspaceAsync(request, context.CliCommand, context.CliEnvironment, readyUrl, assert, cancellationToken);
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
        string cliCommand,
        IReadOnlyDictionary<string, string>? cliEnvironment,
        string serverUrl,
        FileAssertions assert,
        CancellationToken cancellationToken)
    {
        var repo = Path.Join(request.WorkDirectory, "example-workspace");
        Directory.CreateDirectory(repo);
        await File.WriteAllTextAsync(Path.Join(repo, "agent-up.json"), """
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

        var environment = MergeEnvironment(cliEnvironment, "AGENTUP_SERVER_URL", serverUrl);
        var start = await _commands.RunAsync(CliCommand(cliCommand, "start", repo, environment), cancellationToken);
        await File.WriteAllTextAsync(Path.Join(request.WorkDirectory, "cli-start.log"), start.Stdout + start.Stderr, cancellationToken);
        if (start.ExitCode != 0 || !start.Stdout.Contains("Started workspace \"Installed Service Smoke Workspace\"", StringComparison.Ordinal))
            assert.Error("installed.cli.start", $"CLI start failed or returned unexpected output: {start.Stderr}{start.Stdout}");

        var status = await _commands.RunAsync(CliCommand(cliCommand, "status", repo, environment), cancellationToken);
        await File.WriteAllTextAsync(Path.Join(request.WorkDirectory, "cli-status.log"), status.Stdout + status.Stderr, cancellationToken);
        if (status.ExitCode != 0
            || !status.Stdout.Contains("Name:       Installed Service Smoke Workspace", StringComparison.Ordinal)
            || !status.Stdout.Contains("State:      Running", StringComparison.Ordinal))
            assert.Error("installed.cli.status", $"CLI status failed or returned unexpected output: {status.Stderr}{status.Stdout}");
    }

    private static Dictionary<string, string> MergeEnvironment(
        IReadOnlyDictionary<string, string>? source,
        string key,
        string value)
    {
        var environment = source is null
            ? []
            : new Dictionary<string, string>(source, StringComparer.Ordinal);
        environment[key] = value;
        return environment;
    }

    private static CommandSpec CliCommand(InstalledServiceContext context, string argument)
        => CliCommand(context.CliCommand, argument, null, context.CliEnvironment);

    private static CommandSpec CliCommand(
        string command,
        string argument,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environment)
        => command == "cmd.exe"
            ? new CommandSpec("cmd.exe", ["/C", "agent-up.cmd", argument], workingDirectory, environment)
            : new CommandSpec(command, [argument], workingDirectory, environment);

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
