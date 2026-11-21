using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>
/// Wraps an ACP agent launch so the process runs with the team member's Linux permission set
/// rather than the Server's: only the paths the member is granted exist, the clone is writable
/// only for an author, and the Server's own secrets never reach the process environment.
/// </summary>
public interface IAgentSandbox
{
    /// <exception cref="AgentSandboxUnavailableException">The host cannot sandbox agents.</exception>
    AgentSandboxLaunch Prepare(
        AgentCommand command,
        IReadOnlyDictionary<string, string> environment,
        string worktree,
        TeamMemberGrants grants);
}
