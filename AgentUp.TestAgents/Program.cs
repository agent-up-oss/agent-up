using AgentUp.TestAgents.Features.Host.Controllers;
using AgentUp.TestAgents.Features.Host.Services;

// Test agents: real processes implementing each sign-in shape the vendor agent CLIs use, so the
// whole Agent-Up authentication path can be exercised end to end without signing in to Claude,
// ChatGPT, or Cursor. Which agent this is comes from the name it was published under.
using var lifetime = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    lifetime.Cancel();
};

try
{
    var programName = Environment.GetCommandLineArgs().FirstOrDefault() ?? string.Empty;
    var command = TestAgentCommandParser.Parse(programName, args);
    return await new TestAgentHostService().RunAsync(command, lifetime.Token);
}
catch (InvalidOperationException exception)
{
    await Console.Error.WriteLineAsync(exception.Message);
    return 2;
}
