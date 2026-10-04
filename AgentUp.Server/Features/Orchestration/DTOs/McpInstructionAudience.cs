namespace AgentUp.Server.Features.Orchestration.DTOs;

/// <summary>
/// Which addressing rules the connected MCP client has to follow. The distinction that matters is
/// whether the caller can see the filesystem the Server opens paths on, not which client it is.
/// </summary>
public enum McpInstructionAudience
{
    SharedFilesystem,
    Remote,
    Pinned
}
