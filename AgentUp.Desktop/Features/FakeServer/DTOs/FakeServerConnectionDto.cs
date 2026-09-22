namespace AgentUp.Desktop.Features.FakeServer.DTOs;

public sealed record FakeServerConnectionDto(
    string Id,
    string Url,
    string DisplayName,
    bool IsActive);

public sealed record FakeBackendRequestDto(
    string Method,
    string Path,
    string Query,
    string? Body);

public sealed record FakeBackendResponseDto(
    int Status,
    string ContentType,
    string? Body = null,
    bool KeepOpen = false);
