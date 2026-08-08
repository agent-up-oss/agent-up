using AgentUp.Server.Features.Orchestration.Interfaces;

namespace AgentUp.Server.Features.Orchestration.Providers;

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

        When the user asks to "deploy my app with Agent-Up", "run my app with Agent-Up", "start this workspace", "bring up the app", "serve this repo", or "open the app in Agent-Up", use the registered Agent-Up MCP tools. For those requests, call start_workspace with the absolute repository/worktree path immediately instead of listing workspaces, checking status, curling the Server, shelling through the CLI, or starting application commands directly.

        Use list_workspaces and get_workspace_status only when you need to choose among existing workspaces, inspect an already-running workspace, or answer an explicit status/list question. Use stop_workspace when the user asks Agent-Up to stop, shut down, or halt a managed workspace.

        Agent-Up validation is a feedback loop. After start_workspace, use the returned workspace id and allocated ports directly for browser MCP navigation. If browser navigation, inspection, waiting, screenshots, or interaction fails or times out, inspect the workspace console immediately through get_workspace_console on Orchestration MCP. If that tool is unavailable, query Audit MCP for recent application console events with kind=application and source=process for the workspace. Treat console output as the first diagnostic source; it often shows missing dependencies, failed commands, port binding errors, Docker startup failures, or build/runtime crashes. Fix the concrete console issue, then call start_workspace again and repeat the browser validation.

        Before starting a new coding task, call guard_commits for the current repository/worktree. If it fails, stop instead of making new changes unless the user explicitly asked to inspect, debug, or continue the existing queued or working-tree changes.

        At the end of every coding task, use enqueue_commit to declare each logical vertical-slice commit, or enqueue_review_fix_commit when fixing one pull request review issue violation. Then use get_commits_status so the developer can see the queue. Do not run git add, git commit, or git stash. Use one enqueue call per logical slice, and one review-fix enqueue call per review issue id. Scope every conventional commit message to the queued slice, for example fix(Commits): validate queue metadata. Use the correct conventional commit prefix: feat means a user-facing addition, fix means a user-facing fix, test means test-only or smoke-validation changes, chore means maintenance/packaging/CI/tooling with no customer runtime effect, refactor means internal source changes with no behavior change, style means CSS/HTML only, and docs means documentation only including README and similar docs. If agent-up.json contains prompts.commitPolicy, follow that repository-specific policy when choosing prefixes and grouping commits. Cross-slice guidance or documentation updates must be queued in a separate guidance/docs entry instead of being bundled into an implementation slice. When feature-sliced paths are recognized, MCP rejects cross-slice file groups and mismatched slice labels. Mutating queue operations are blocked while Git has an active merge, rebase, cherry-pick, revert, or bisect. Use commit queue MCP tools for enqueue, queue inspection, metadata edits, file assignment, edit sessions, archive/restore, clear, and guard operations instead of shelling through commit CLI commands. enqueue_commit intentionally restores tracked files to their pre-change state after saving the patch; do not re-apply or modify those files after a successful enqueue because the queue owns them until the developer runs agentup commits next.
        """;

    public string GetAgentUpJsonFormat() =>
        """
        agent-up.json is the declarative workspace configuration file at a repository or worktree root.

        Current format:

        {
          "name": "Inventory",
          "display": {
            "name": "Agent 1 - Checkout",
            "branch": "Frontend polish"
          },
          "applications": [
            {
              "name": "Frontend",
              "command": "npm run dev",
              "path": "/",
              "ports": [
                {
                  "variable": "WEB_PORT",
                  "defaultPort": 5173,
                  "protocol": "http",
                  "healthCheck": "/health"
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
          ],
          "prompts": {
            "commitPolicy": "Scope commit messages to the queued slice. Use fix for customer-facing production behavior changes, feat for customer-facing additions, test for test-only or smoke-validation changes, refactor for no-behavior source changes, and chore only for maintenance, packaging, CI, or tooling changes with no customer runtime effect."
          }
        }

        Fields:

        name: Display name for the workspace project.
        display.name: Optional Desktop workspace title override. It does not change repository or worktree path handling.
        display.branch: Optional Desktop workspace subtitle override. It does not change Git branch detection, audit identity, or worktree handling.
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
        prompts.commitPolicy: Optional repository-specific commit guidance for AI agents. The default policy scopes commit messages to the queued slice; uses feat for user-facing additions, fix for user-facing fixes, test for test-only or smoke-validation changes, refactor for no-behavior source changes, chore for maintenance, packaging, CI, or tooling with no customer runtime effect, style for CSS/HTML only, and docs for documentation-only changes.
        ports[].variable: Environment variable that receives the allocated port.
        ports[].defaultPort: Preferred/default port used to derive allocation intent.
        ports[].protocol: Protocol label, usually http or tcp.
        ports[].healthCheck: Optional HTTP path the server probes every 5 seconds (e.g. "/health"). When present the port LED shows Checking (amber) until the path responds, then Healthy (green). Absence means the desktop uses a local TCP probe instead.

        If agent-up.json is missing, inspect docs/user-docs/agent-up-json.md, search the repository for an existing agent-up.json, or ask the user before creating one.
        """;
}
