namespace AgentUp.Server.Features.Orchestration.DTOs;

public sealed record McpInstructionContext(McpInstructionAudience Audience, string? WorkspaceId)
{
    public static McpInstructionContext SharedFilesystem { get; } =
        new(McpInstructionAudience.SharedFilesystem, null);

    public static McpInstructionContext Remote { get; } =
        new(McpInstructionAudience.Remote, null);

    public static McpInstructionContext Pinned(string workspaceId)
        => new(McpInstructionAudience.Pinned, workspaceId);
}
