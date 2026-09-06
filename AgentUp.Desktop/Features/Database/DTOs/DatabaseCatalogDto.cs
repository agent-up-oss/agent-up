namespace AgentUp.Desktop.Features.Database.DTOs;

public sealed record DatabaseCatalogDto(IReadOnlyList<DatabaseDto> Databases);
public sealed record DatabaseDto(string Name, IReadOnlyList<DatabaseTableDto> Tables);
public sealed record DatabaseTableDto(string Schema, string Name)
{
    public string DisplayName => $"{Schema}.{Name}";
}
