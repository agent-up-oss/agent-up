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
    {
        if (args.Any(arg => arg == "--version"))
            return PrintVersion();

        var command = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "";

        return command switch
        {
            "version" => PrintVersion(),
            "start"  => await _start.RunAsync(),
            "stop"   => await _stop.RunAsync(),
            "list"   => await _list.RunAsync(),
            "status" => await _status.RunAsync(),
            _        => PrintHelp()
        };
    }

    private int PrintHelp()
    {
        _output.WriteLine("Usage: agent-up <command> [--server <url>]");
        _output.WriteLine("Commands:");
        _output.WriteLine("  start   Read agent-up.json and launch all applications");
        _output.WriteLine("  stop    Stop all running applications for the current workspace");
        _output.WriteLine("  list    List all workspaces on the server");
        _output.WriteLine("  status  Show status of the current workspace");
        _output.WriteLine("  version Print the CLI version");
        _output.WriteLine();
        _output.WriteLine("Options:");
        _output.WriteLine("  --version       Print the CLI version");
        _output.WriteLine("  --server <url>  Server URL (default: $AGENTUP_SERVER_URL or http://localhost:5000)");
        return 0;
    }

    private int PrintVersion()
    {
        var version = typeof(WorkspacesController).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? typeof(WorkspacesController).Assembly.GetName().Version?.ToString()
                      ?? "0.0.0";
        _output.WriteLine(version);
        return 0;
    }
}
