using AgentUp.CLI.Composition;
using AgentUp.InstallerConfig;

RepositoryDotEnv.LoadOptional();

if (IsAuthCommand(args) && !HasExplicitServerArg(args))
{
    Console.Error.WriteLine("auth commands require --server <url>");
    return 1;
}

var serverUrl = GetServerUrl(args);
var runner = CliRunnerFactory.Create(serverUrl, Directory.GetCurrentDirectory());
return await runner.RunAsync(args);

static bool IsAuthCommand(string[] args)
    => args.Any(static arg => arg.Equals("auth", StringComparison.OrdinalIgnoreCase));

static bool HasExplicitServerArg(string[] args)
    => Array.IndexOf(args, "--server") >= 0 && Array.IndexOf(args, "--server") + 1 < args.Length;

static string GetServerUrl(string[] args)
{
    var idx = Array.IndexOf(args, "--server");
    if (idx >= 0 && idx + 1 < args.Length)
        return args[idx + 1];
    return Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL") ?? "http://localhost:5000";
}
