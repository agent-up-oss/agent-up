namespace AgentUp.Desktop.Features.Authentication.DTOs;

public sealed record SavedServerListDto(IReadOnlyList<SavedServerDto> Servers, string CurrentUrl);
