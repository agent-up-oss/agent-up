namespace AgentUp.Desktop.Features.Authentication.DTOs;

public sealed record AuthenticationResponse(bool AuthenticationRequired, string? AccessToken);
