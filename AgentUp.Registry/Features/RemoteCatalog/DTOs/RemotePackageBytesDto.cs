namespace AgentUp.Registry.Features.RemoteCatalog.DTOs;

public sealed record RemotePackageBytesDto(string Id, string Version, byte[] Archive);
