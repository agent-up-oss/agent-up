namespace AgentUp.CLI.Features.Capabilities.Models;

public static class CapabilitiesHelp
{
    public const string Text =
        """
        Usage: agent-up capabilities <command>

          list                         Show id, version, enabled, canRun, and why it cannot run
          enable <id> [--version <v>]  Enable a packed capability package on the Server
          disable <id>                 Disable a capability package
          bootstrap                    Pack first-party capabilities and enable them
        """;
}
