using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
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

            await RunRequiredAsync(assert, CliCommand(context, request.Product.CliShimName, "--version"), "installed.cli.version", cancellationToken);
            var readyUrl = await _serverProbe.WaitForReadyAsync(
                request.PrimaryServerUrl,
                request.FallbackServerUrl,
                Path.Join(request.WorkDirectory, "service-workspaces-before.json"),
                cancellationToken);

            if (readyUrl is null)
            {
                assert.Error("installed.server.ready", $"{request.Product.ServiceName} did not become ready at {request.PrimaryServerUrl} or {request.FallbackServerUrl}.");
                await RunDiagnosticsAsync(context, cancellationToken);
                return new InstalledServiceSmokeResult(null, assert.Findings);
            }

            await _securityChecks.RunAsync(readyUrl, assert, cancellationToken);
            await SmokeCliWorkspaceAsync(request, context.CliCommand, context.CliEnvironment, readyUrl, assert, request.Product.CliShimName, cancellationToken);
            if (Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL") != "1")
            {
                using var capabilitySmoke = new CapabilityLifecycleSmoke(_commands);
                await capabilitySmoke.RunAsync(request.WorkDirectory, context, readyUrl, assert, cancellationToken);
            }

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
        string shimName,
        CancellationToken cancellationToken)
    {
        var configFileName = request.Product.WorkspaceConfigFileName;
        var repo = Path.Join(request.WorkDirectory, "example-workspace");
        Directory.CreateDirectory(repo);
        var configPath = SafeWorkspaceConfigPath(repo, configFileName);
        await File.WriteAllTextAsync(configPath, """
            {
              "name": "Installed Service Smoke Workspace",
              "applications": []
            }
            """, cancellationToken);

        await RunRequiredAsync(assert, GitCommand(repo, GitSmokeCommand.Init, configFileName), "installed.git.init", cancellationToken);
        await RunRequiredAsync(assert, GitCommand(repo, GitSmokeCommand.Email, configFileName), "installed.git.email", cancellationToken);
        await RunRequiredAsync(assert, GitCommand(repo, GitSmokeCommand.Name, configFileName), "installed.git.name", cancellationToken);
        await RunRequiredAsync(assert, GitCommand(repo, GitSmokeCommand.Add, configFileName), "installed.git.add", cancellationToken);
        await RunRequiredAsync(assert, GitCommand(repo, GitSmokeCommand.Commit, configFileName), "installed.git.commit", cancellationToken);

        var environment = MergeEnvironment(cliEnvironment, "AGENTUP_SERVER_URL", serverUrl);
        var start = await _commands.RunAsync(CliCommand(cliCommand, shimName, "start", repo, environment), cancellationToken);
        await File.WriteAllTextAsync(Path.Join(request.WorkDirectory, "cli-start.log"), start.Stdout + start.Stderr, cancellationToken);
        if (start.ExitCode != 0 || !start.Stdout.Contains("Started workspace \"Installed Service Smoke Workspace\"", StringComparison.Ordinal))
            assert.Error("installed.cli.start", $"CLI start failed or returned unexpected output: {start.Stderr}{start.Stdout}");

        var status = await _commands.RunAsync(CliCommand(cliCommand, shimName, "status", repo, environment), cancellationToken);
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

    private static CommandSpec CliCommand(InstalledServiceContext context, string shimName, string argument)
        => CliCommand(context.CliCommand, shimName, argument, null, context.CliEnvironment);

    private static CommandSpec CliCommand(
        string command,
        string shimName,
        string argument,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environment)
    {
        if (workingDirectory is null)
        {
            return command == "cmd.exe"
                ? new CommandSpec("cmd.exe", ["/C", $"{shimName}.cmd", argument], Environment: environment)
                : new CommandSpec(command, [argument], Environment: environment);
        }

        var workingEnvironment = MergeEnvironment(environment, WorkingDirectoryEnvironmentKey, workingDirectory);
        if (command == "cmd.exe")
        {
            var shellCommand = argument switch
            {
                "start" => $"Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; {shimName}.cmd start",
                "status" => $"Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; {shimName}.cmd status",
                _ => throw new ArgumentOutOfRangeException(nameof(argument), argument, "Unsupported CLI smoke command.")
            };

            return new CommandSpec("powershell.exe", ["-NoProfile", "-Command", shellCommand], Environment: workingEnvironment);
        }

        var unixShellCommand = argument switch
        {
            "start" => $"cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && {shimName} start",
            "status" => $"cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && {shimName} status",
            _ => throw new ArgumentOutOfRangeException(nameof(argument), argument, "Unsupported CLI smoke command.")
        };

        return new CommandSpec("bash", ["-lc", unixShellCommand], Environment: workingEnvironment);
    }

    private static CommandSpec GitCommand(string workingDirectory, GitSmokeCommand command, string configFileName)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkingDirectoryEnvironmentKey] = workingDirectory
        };

        return OperatingSystem.IsWindows()
            ? new CommandSpec("powershell.exe", ["-NoProfile", "-Command", WindowsGitCommand(command, configFileName)], Environment: environment)
            : new CommandSpec("bash", ["-lc", UnixGitCommand(command, configFileName)], Environment: environment);
    }

    private static string SafeWorkspaceConfigPath(string repositoryDirectory, string configFileName)
    {
        if (string.IsNullOrWhiteSpace(configFileName)
            || Path.IsPathRooted(configFileName)
            || configFileName.Contains("..", StringComparison.Ordinal)
            || configFileName.IndexOfAny(['/', '\\', ':']) >= 0
            || !string.Equals(configFileName, Path.GetFileName(configFileName), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Workspace config file name must be a single safe file name.");
        }

        var repositoryRoot = Path.GetFullPath(repositoryDirectory);
        var configPath = Path.GetFullPath(Path.Join(repositoryRoot, configFileName));
        if (!configPath.StartsWith(repositoryRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Workspace config file path escaped the workspace repository.");

        return configPath;
    }

    private static string UnixGitCommand(GitSmokeCommand command, string configFileName)
        => command switch
        {
            GitSmokeCommand.Init => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git init -q",
            GitSmokeCommand.Email => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git config user.email smoke@ci.local",
            GitSmokeCommand.Name => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git config user.name \"Smoke CI\"",
            GitSmokeCommand.Add => $"cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git add {configFileName}",
            GitSmokeCommand.Commit => "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git commit -q -m \"Add service smoke workspace\"",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported git smoke command.")
        };

    private static string WindowsGitCommand(GitSmokeCommand command, string configFileName)
        => command switch
        {
            GitSmokeCommand.Init => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git init -q",
            GitSmokeCommand.Email => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git config user.email smoke@ci.local",
            GitSmokeCommand.Name => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git config user.name \"Smoke CI\"",
            GitSmokeCommand.Add => $"Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git add {configFileName}",
            GitSmokeCommand.Commit => "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git commit -q -m \"Add service smoke workspace\"",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported git smoke command.")
        };

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

    public virtual void Dispose()
    {
        if (_securityChecks is IDisposable disposable)
            disposable.Dispose();
    }

    private const string WorkingDirectoryEnvironmentKey = "AGENTUP_SMOKE_WORKING_DIRECTORY";

    private enum GitSmokeCommand
    {
        Init,
        Email,
        Name,
        Add,
        Commit
    }
}
