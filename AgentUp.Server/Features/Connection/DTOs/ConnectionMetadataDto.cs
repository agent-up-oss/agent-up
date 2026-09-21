namespace AgentUp.Server.Features.Connection.DTOs;

public sealed record ConnectionMetadataDto(
    string ApiVersion,
    string ConnectionId,
    string Kind,
    string DisplayName,
    ConnectionAuthenticationDto Authentication,
    string WorkspacePresentation);
