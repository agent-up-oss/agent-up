namespace AgentUp.Server.Features.Database.DTOs;

public sealed record DatabaseQueryResultDto(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows,
    int AffectedRows);
