namespace AgentUp.AUDebug.Features.Host.DTOs;

public sealed record HostedProcessDto(string Name, int Pid, string LogPath, string? Url);
