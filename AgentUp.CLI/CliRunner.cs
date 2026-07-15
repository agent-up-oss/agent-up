using AgentUp.CLI.Commands;
using AgentUp.CLI.Http;
using System.Reflection;

namespace AgentUp.CLI;

public sealed class CliRunner
{
    private readonly string _serverUrl;
    private readonly string _workingDirectory;
    private readonly TextWriter _output;

    public CliRunner(string serverUrl, string workingDirectory, TextWriter? output = null)
    {
        _serverUrl = serverUrl;
        _workingDirectory = workingDirectory;
        _output = output ?? Console.Out;
    }

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Any(arg => arg == "--version"))
            return PrintVersion();

        var command = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "";
        using var http = new HttpClient { BaseAddress = new Uri(_serverUrl) };
        var client = new WorkspaceApiClient(http);

        return command switch
        {
            "version" => PrintVersion(),
            "start"  => await new StartCommand(client, _workingDirectory, _output).RunAsync(),
            "stop"   => await new StopCommand(client, _workingDirectory, _output).RunAsync(),
            "list"   => await new ListCommand(client, _output).RunAsync(),
            "status" => await new StatusCommand(client, _workingDirectory, _output).RunAsync(),
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
        var version = typeof(CliRunner).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? typeof(CliRunner).Assembly.GetName().Version?.ToString()
                      ?? "0.0.0";
        _output.WriteLine(version);
        return 0;
    }
}
