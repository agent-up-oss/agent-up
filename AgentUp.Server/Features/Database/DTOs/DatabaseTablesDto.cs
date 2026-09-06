namespace AgentUp.Server.Features.Database.DTOs;

public sealed record DatabaseTablesDto(IReadOnlyList<string> Tables);
