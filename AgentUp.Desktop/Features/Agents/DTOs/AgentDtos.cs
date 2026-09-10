using System.Text.Json;

namespace AgentUp.Desktop.Features.Agents.DTOs;

public sealed record AgentDescriptorDto(string Agent, bool Available, string DisplayName);
public sealed record AgentSessionDto(string WorkspaceId, string? Agent, string State, string? SessionId, string? Error, IReadOnlyList<AgentDescriptorDto> Agents);
public sealed record AgentEventDto(long Sequence, string Type, JsonElement Payload, DateTimeOffset Timestamp);
