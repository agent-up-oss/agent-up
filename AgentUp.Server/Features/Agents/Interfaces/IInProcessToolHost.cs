using System.Text.Json;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>
/// The only way an in-process agent touches anything. Tools are listed and called for one
/// workspace, narrowed to that workspace's team member grant, with the workspace pinned.
/// </summary>
public interface IInProcessToolHost
{
    Task<IReadOnlyList<InProcessTool>> ListAsync(string workspaceId, CancellationToken cancellationToken);

    Task<InProcessToolResult> CallAsync(
        string workspaceId,
        string tool,
        JsonElement arguments,
        CancellationToken cancellationToken);
}
