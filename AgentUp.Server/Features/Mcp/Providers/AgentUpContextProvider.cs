using AgentUp.Server.Features.Mcp.Interfaces;

namespace AgentUp.Server.Features.Mcp.Providers;

public sealed class AgentUpContextProvider : IAgentUpContextProvider
{
    public string GetAgentUpContext() =>
        """
        Agent-Up manages local AI-assisted development workspaces. Agent-Up is not an application framework, cloud deployment tool, IDE, or application orchestrator.

        AgentUp.Server is the single source of truth. Desktop, CLI, MCP clients, and future integrations are clients of the Server. They may display state and request actions, but they must not own runtime state or duplicate orchestration logic.

        The Server owns workspace registry, process lifecycle, ports, Docker lifecycle, browser lifecycle, browser profiles, diagnostics, event history, MCP, and REST APIs.

        Applications are described declaratively with agent-up.json. Applications must not reference Agent-Up packages, SDKs, or APIs. Agent-Up injects runtime values through environment variables and process launch configuration.

        Local application commands are opaque to Agent-Up and run through the host platform shell: cmd.exe /C on Windows and Bash on Unix-like systems.

        The Server owns all ports. Applications consume only environment variables declared in agent-up.json, such as WEB_PORT, API_PORT, AUTH_PORT, or service-specific variables.

        When the user asks to "deploy my app with Agent-Up", "run my app with Agent-Up", "start this workspace", "bring up the app", "serve this repo", or "open the app in Agent-Up", use the registered Agent-Up MCP tools. For those requests, call start_workspace with the absolute repository/worktree path instead of curling the Server, shelling through the CLI, or starting application commands directly.

        Use list_workspaces and get_workspace_status to inspect existing Agent-Up-managed workspaces. Use stop_workspace when the user asks Agent-Up to stop, shut down, or halt a managed workspace.
        """;

    public string GetAgentUpJsonFormat() =>
        """
        agent-up.json is the declarative workspace configuration file at a repository or worktree root.

        Current format:

        {
          "name": "Inventory",
          "applications": [
            {
              "name": "Frontend",
              "command": "npm run dev",
              "path": "/",
              "ports": [
                {
                  "variable": "WEB_PORT",
                  "defaultPort": 5173,
                  "protocol": "http"
                }
              ]
            }
          ],
          "services": [
            {
              "name": "Database",
              "image": "postgres:16",
              "ports": [
                {
                  "variable": "POSTGRES_PORT",
                  "defaultPort": 5432,
                  "protocol": "tcp"
                }
              ],
              "environment": {
                "POSTGRES_PASSWORD": "not-a-real-value"
              },
              "volumes": [
                "pgdata:/var/lib/postgresql/data"
              ]
            }
          ]
        }

        Fields:

        name: Display name for the workspace project.
        applications: Local application processes the Server can launch.
        applications[].name: Display name for the application.
        applications[].command: Opaque shell command used to start the application.
        applications[].path: Browser path to open for the application.
        applications[].ports: Port declarations for the application.
        services: Docker services the Server can start for the workspace.
        services[].name: Display name for the service.
        services[].image: Docker image for the service.
        services[].ports: Port declarations for the service.
        services[].environment: Optional environment variables for the service.
        services[].volumes: Optional Docker volume mappings for the service.
        ports[].variable: Environment variable that receives the allocated port.
        ports[].defaultPort: Preferred/default port used to derive allocation intent.
        ports[].protocol: Protocol label, usually http or tcp.

        If agent-up.json is missing, inspect docs/user-docs/agent-up-json.md, search the repository for an existing agent-up.json, or ask the user before creating one.
        """;
}
