namespace AgentUp.Server.Features.Connection.DTOs;

public sealed record ConnectionAuthenticationDto(
    string Mode,
    string Prompt,
    bool IdentifierRequired);
