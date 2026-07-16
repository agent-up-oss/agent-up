using AgentUp.CLI.Features.Workspaces.Controllers;

var serverUrl = GetServerUrl(args);
var runner = new CliRunner(serverUrl, Directory.GetCurrentDirectory());
return await runner.RunAsync(args);

static string GetServerUrl(string[] args)
{
    var idx = Array.IndexOf(args, "--server");
    if (idx >= 0 && idx + 1 < args.Length)
        return args[idx + 1];
    return Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL") ?? "http://localhost:5000";
}
