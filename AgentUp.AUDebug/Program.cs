using AgentUp.AUDebug.Composition;
using AgentUp.InstallerConfig;

RepositoryDotEnv.LoadOptional();

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

var runner = AuDebugRunnerFactory.Create(Directory.GetCurrentDirectory());
return await runner.RunAsync(args, shutdown.Token);
