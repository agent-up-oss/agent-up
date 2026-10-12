using System.Text.Json;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>
/// Resolves an ACP permission request without a human. A null decision is a typed
/// <c>blocked:policy</c> rather than a hang on the permission queue.
/// </summary>
public interface IAgentPermissionPolicy
{
    PermissionDecision? Decide(JsonElement request);
}
