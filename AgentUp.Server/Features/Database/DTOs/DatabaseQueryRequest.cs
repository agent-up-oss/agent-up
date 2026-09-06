namespace AgentUp.Server.Features.Database.DTOs;

public sealed record DatabaseQueryRequest(string Database, string Sql);
