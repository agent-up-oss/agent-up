using AgentUp.Server.Features.Applications.DTOs;

namespace AgentUp.Server.Features.Orchestration.DTOs;

public sealed record AgentUpConfiguration(
    string Name,
    IReadOnlyList<ApplicationDefinition>? Applications = null,
    IReadOnlyList<DockerServiceDefinition>? Services = null,
    AgentPromptConfiguration? Prompts = null,
    WorkspaceDisplayConfiguration? Display = null);

public sealed record AgentPromptConfiguration(
    string? CommitPolicy = null);

public sealed record WorkspaceDisplayConfiguration(
    string? Name = null,
    string? Branch = null);
