using System.Reflection;
using AgentUp.CLI.Features.Commits.Controllers;

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
    private readonly CommitsController _commits;

    public WorkspacesController(
        string serverUrl,
        TextWriter output,
        StartCommand start,
        StopCommand stop,
        ClearCommand clear,
        ListCommand list,
        StatusCommand status,
        CommitsController commits)
    {
        _serverUrl = serverUrl;
        _output = output;
        _start = start;
        _stop = stop;
        _clear = clear;
        _list = list;
        _status = status;
        _commits = commits;
    }

    public async Task<int> RunAsync(string[] args)
        => args.Any(arg => arg == "--version")
            ? PrintVersion(_output)
            : await ResolveCommand(args, _serverUrl, _start, _stop, _clear, _list, _status, _commits, _output)();

    private static Func<Task<int>> ResolveCommand(
        string[] args,
        string serverUrl,
        StartCommand start,
        StopCommand stop,
        ClearCommand clear,
        ListCommand list,
        StatusCommand status,
        CommitsController commits,
        TextWriter output)
        => (args.FirstOrDefault(argument => !argument.StartsWith("--")) ?? "") switch
        {
            "version" => () => Task.FromResult(PrintVersion(output)),
            "start" => start.RunAsync,
            "stop" => stop.RunAsync,
            "clear" => clear.RunAsync,
            "list" => list.RunAsync,
            "status" => status.RunAsync,
            "commits" => () => commits.RunAsync(args.SkipWhile(a => a != "commits").Skip(1).ToArray()),
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
        output.WriteLine("  commits  Manage the vertical-slice commit queue");
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
