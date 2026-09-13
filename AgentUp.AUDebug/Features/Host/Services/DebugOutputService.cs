using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Features.Host.Services;

public sealed class DebugOutputService
{
    private readonly TextWriter _output;

    public DebugOutputService(TextWriter output) => _output = output;

    public int Write(CommandResultDto result)
    {
        _output.WriteLine(result.Message);
        if (result.ArtifactPath is not null)
            _output.WriteLine($"screenshot: {result.ArtifactPath}");
        return result.ExitCode;
    }

    public int WriteError(string message)
    {
        _output.WriteLine(message);
        return 1;
    }

    public void WriteMessage(string message) => _output.WriteLine(message);

    public int WriteHelp()
    {
        _output.WriteLine(
            """
            Usage: au-debug <command> [--timeout 30] [--detach] [--password <pw>]

            Host:
              up [--detach]              Start Server, Desktop, Mobile, and docs. Streams process logs
                                         until Ctrl+C or `au-debug down`. Readiness uses a 30s watchdog.
              down                       Stop every process started by au-debug up.
              status                     Report whether Server, Desktop, Mobile, and docs are ready.

            Desktop:
              desktop screenshot         Capture the Agent-Up window and print the file path.
              desktop login              Type AGENTUP_ADMIN_PASSWORD into Desktop and click Sign in.
              desktop start-workspace <name>
                                         Start the named workspace on the repo Server and screenshot Desktop.
              desktop open-agent         Click the Agent tab in Desktop and screenshot it.

            Mobile:
              mobile screenshot          Capture Mobile web and print the file path.
              mobile login               Drive the Mobile connect/login screen against the repo Server.
              mobile open-agent <name>   Open the named workspace agent route and screenshot it.

            Docs:
              docs screenshot            Capture the design-system docs page and print the file path.

            Tests:
              test                       Run every visual-iteration suite.
              test all                   Same as test.
              test <suite>               Run one suite: design-system, desktop, mobile,
                                         au-debug, or architecture.

            Builds:
              build                      Rebuild design-system dist and Mobile web export.
              build all                  Same as build.
              build design-system        npm run build in AgentUp.DesignSystem.
              build mobile               npm run typecheck and npm run build:web in AgentUp.Mobile.

            Options:
              --timeout <seconds>        Watchdog for readiness, tests, and one-shot commands.
                                         Default 30s; test and build default to 180s, all to 600s.
              --detach                   After up is ready, return without following logs.
              --password <pw>            Admin password for login commands (else $AGENTUP_ADMIN_PASSWORD).
            """);
        return 0;
    }
}
