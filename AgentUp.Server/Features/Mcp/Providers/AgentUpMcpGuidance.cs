namespace AgentUp.Server.Features.Mcp.Providers;

public static class AgentUpMcpGuidance
{
    public const string ServerInstructions =
        """
        Agent-Up is the registered MCP server for managing local AI-assisted development workspaces. Use these Agent-Up MCP tools whenever the user asks to use Agent-Up, agent up, the Agent-Up server, or the Agent-Up workspace manager.

        Treat phrases such as "deploy my app with Agent-Up", "run my app with Agent-Up", "start this workspace", "bring up the app", "serve this repo", or "open the app in Agent-Up" as requests to register or update the current repository/worktree from agent-up.json and call start_workspace with its absolute path. Agent-Up does not deploy to cloud infrastructure; it starts and manages the local development environment.

        Before using curl, shelling through the Agent-Up CLI, or starting application commands directly, prefer the MCP tools here when they can perform the requested Agent-Up operation. Use list_workspaces or get_workspace_status to discover existing registered workspaces, and use get_agent_up_context or get_agent_up_json_format when you need Agent-Up rules or configuration shape.
        """;
}
