namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// How a team member's agent runs. Authors and dev reviewers work on code, so they run an ACP
/// coding CLI (Claude, Codex, Cursor) against their checkout. PM reviewers have nothing to do
/// with code and skip that layer: their agent runs inside the Server and reaches the workspace
/// only through Agent-Up MCP tools.
/// </summary>
public enum AgentFixtureKind
{
    CodingAgent,
    InProcess
}
