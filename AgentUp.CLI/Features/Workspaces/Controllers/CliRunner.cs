using System.Reflection;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class WorkspacesController
{
    private readonly string _serverUrl;
    private readonly TextWriter _output;
    private readonly StartCommand _start;
    private readonly StopCommand _stop;
    private readonly ListCommand _list;
    private readonly StatusCommand _status;

    public WorkspacesController(
        string serverUrl,
        TextWriter output,
        StartCommand start,
        StopCommand stop,
        ListCommand list,
        StatusCommand status)
    {
        _serverUrl = serverUrl;
        _output = output;
        _start = start;
        _stop = stop;
        _list = list;
        _status = status;
    }

    public async Task<int> RunAsync(string[] args)
        => args.Any(arg => arg == "--version")
            ? PrintVersion(_output)
            : await ResolveCommand(args, _start, _stop, _list, _status, _output)();

    private static Func<Task<int>> ResolveCommand(
        string[] args,
        StartCommand start,
        StopCommand stop,
        ListCommand list,
        StatusCommand status,
        TextWriter output)
        => (args.FirstOrDefault(argument => !argument.StartsWith("--")) ?? "") switch
        {
            "version" => () => Task.FromResult(PrintVersion(output)),
            "start" => start.RunAsync,
            "stop" => stop.RunAsync,
            "list" => list.RunAsync,
            "status" => status.RunAsync,
            _ => () => Task.FromResult(PrintHelp(output))
        };

    private static int PrintHelp(TextWriter output)
    {
        output.WriteLine("Usage: agent-up <command> [--server <url>]");
        output.WriteLine("Commands:");
        output.WriteLine("  start   Read agent-up.json and launch all applications");
        output.WriteLine("  stop    Stop all running applications for the current workspace");
        output.WriteLine("  list    List all workspaces on the server");
        output.WriteLine("  status  Show status of the current workspace");
        output.WriteLine("  version Print the CLI version");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --version       Print the CLI version");
        output.WriteLine("  --server <url>  Server URL (default: $AGENTUP_SERVER_URL or http://localhost:5000)");
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
