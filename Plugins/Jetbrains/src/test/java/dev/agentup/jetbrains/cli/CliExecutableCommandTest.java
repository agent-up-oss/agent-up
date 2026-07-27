package dev.agentup.jetbrains.cli;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

final class CliExecutableCommandTest {
    @Test
    void parseUsesDefaultExecutableForBlankValue() {
        CliExecutableCommand command = CliExecutableCommand.parse(" ");

        assertEquals("agent-up", command.executable());
        assertEquals(0, command.arguments().size());
    }

    @Test
    void parseSplitsDotnetRunCommand() {
        CliExecutableCommand command = CliExecutableCommand.parse(
            "dotnet run --project /home/themassiveone/github/Agent-Up-Workspace/agent-up-agent1/AgentUp.CLI/AgentUp.CLI.csproj"
        );

        assertEquals("dotnet", command.executable());
        assertEquals(
            java.util.List.of(
                "run",
                "--project",
                "/home/themassiveone/github/Agent-Up-Workspace/agent-up-agent1/AgentUp.CLI/AgentUp.CLI.csproj"
            ),
            command.arguments()
        );
    }

    @Test
    void parsePreservesQuotedArguments() {
        CliExecutableCommand command = CliExecutableCommand.parse("\"/tmp/agent up\" --profile 'local test'");

        assertEquals("/tmp/agent up", command.executable());
        assertEquals(java.util.List.of("--profile", "local test"), command.arguments());
    }
}
