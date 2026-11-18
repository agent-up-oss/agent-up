namespace AgentUp.CLI.Features.Capabilities.Models;

public static class CapabilitiesHelp
{
    public const string Text =
        """
        Usage: agent-up capabilities <command>

          list           Show every built-in capability: id, version, enabled, available, canRun, and why it cannot run
          enable <id>    Enable a built-in capability on the Server (refused while its software is not found)
          disable <id>   Disable a capability; it stays installed
          bootstrap      Enable every built-in capability whose software the Server can find
        """;
}
