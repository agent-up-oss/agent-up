using System.Reflection;
using AgentUp.CLI.Features.Authentication.Controllers;
using AgentUp.CLI.Features.Commits.Controllers;
using AgentUp.CLI.Features.Verification.Controllers;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class WorkspacesController
{
    private readonly string _serverUrl;
    private readonly TextWriter _output;
    private readonly StartCommand _start;
    private readonly StopCommand _stop;
    private readonly ClearCommand _clear;
    private readonly ListCommand _list;
    private readonly StatusCommand _status;
    private readonly DiagnosticsCommand _diagnostics;
    private readonly AuthenticationController _authentication;
    private readonly CommitsController _commits;
    private readonly VerificationController _verification;

    public WorkspacesController(
        string serverUrl,
        TextWriter output,
        StartCommand start,
        StopCommand stop,
        ClearCommand clear,
        ListCommand list,
        StatusCommand status,
        DiagnosticsCommand diagnostics,
        AuthenticationController authentication,
        CommitsController commits,
        VerificationController verification)
    {
        _serverUrl = serverUrl;
        _output = output;
        _start = start;
        _stop = stop;
        _clear = clear;
        _list = list;
        _status = status;
        _diagnostics = diagnostics;
        _authentication = authentication;
        _commits = commits;
        _verification = verification;
    }

    public async Task<int> RunAsync(string[] args)
        => args.Any(arg => arg == "--version")
            ? PrintVersion(_output)
            : await ResolveCommand(args, _serverUrl, _start, _stop, _clear, _list, _status, _diagnostics, _authentication, _commits, _verification, _output)();

    private static Func<Task<int>> ResolveCommand(
        string[] args,
        string serverUrl,
        StartCommand start,
        StopCommand stop,
        ClearCommand clear,
        ListCommand list,
        StatusCommand status,
        DiagnosticsCommand diagnostics,
        AuthenticationController authentication,
        CommitsController commits,
        VerificationController verification,
        TextWriter output)
        => (args.FirstOrDefault(argument => !argument.StartsWith("--")) ?? "") switch
        {
            "version" => () => Task.FromResult(PrintVersion(output)),
            "start" => start.RunAsync,
            "stop" => stop.RunAsync,
            "clear" => clear.RunAsync,
            "list" => list.RunAsync,
            "status" => status.RunAsync,
            "diagnostics" => diagnostics.RunAsync,
            "auth" => () => authentication.RunAsync(args.SkipWhile(a => a != "auth").Skip(1).ToArray()),
            "commits" => () => commits.RunAsync(args.SkipWhile(a => a != "commits").Skip(1).ToArray()),
            "verify" => () => verification.RunAsync(args.SkipWhile(a => a != "verify").Skip(1).ToArray()),
            _ => () => Task.FromResult(PrintHelp(output, serverUrl))
        };

    private static int PrintHelp(TextWriter output, string serverUrl)
    {
        output.WriteLine("Usage: agent-up <command> [--server <url>]");
        output.WriteLine("Commands:");
        output.WriteLine("  start    Read agent-up.json and launch all applications");
        output.WriteLine("  stop     Stop all running applications for the current workspace");
        output.WriteLine("  clear    Stop and remove all workspaces on the server");
        output.WriteLine("  list     List all workspaces on the server");
        output.WriteLine("  status   Show status of the current workspace");
        output.WriteLine("  diagnostics  Show workspace process, log, health, and browser diagnostics");
        output.WriteLine("  auth     Authenticate with the server (login, logout, status)");
        output.WriteLine("  commits  Manage the vertical-slice commit queue");
        output.WriteLine("  verify   Plan, run, and guard the checks the current changes require");
        output.WriteLine("  version  Print the CLI version");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --version       Print the CLI version");
        output.WriteLine($"  --server <url>  Server URL (default: $AGENTUP_SERVER_URL or {serverUrl})");
        return 0;
    }

    private static int PrintVersion(TextWriter output)
    {
        var version = typeof(WorkspacesController).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? typeof(WorkspacesController).Assembly.GetName().Version?.ToString()
                      ?? "0.0.0";
        output.WriteLine(version);
        return 0;
    }
}
