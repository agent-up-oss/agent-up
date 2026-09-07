namespace AgentUp.Server.Features.Authentication.DTOs;

public sealed record LoginResponse(bool AuthenticationRequired, string? AccessToken = null);
