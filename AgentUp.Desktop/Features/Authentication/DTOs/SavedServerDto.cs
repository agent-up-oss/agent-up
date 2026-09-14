namespace AgentUp.Desktop.Features.Authentication.DTOs;

public sealed record SavedServerDto(string Id, string Url, bool HasCredential, bool IsActive);
