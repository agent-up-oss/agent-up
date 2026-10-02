namespace AgentUp.Desktop.Features.Authentication.DTOs;

public sealed record ConnectionAuthenticationResponse(
    string? Mode,
    string? Prompt,
    bool IdentifierRequired);
