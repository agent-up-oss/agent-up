namespace AgentUp.Desktop.Features.Authentication.DTOs;

public sealed record ConnectionMetadataResponse(
    string? ApiVersion,
    string? ConnectionId,
    string? Kind,
    string? DisplayName,
    ConnectionAuthenticationResponse? Authentication,
    string? WorkspacePresentation);
