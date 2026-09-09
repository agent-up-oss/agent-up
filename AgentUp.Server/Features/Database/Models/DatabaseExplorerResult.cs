namespace AgentUp.Server.Features.Database.Models;

public enum DatabaseExplorerStatus
{
    Success,
    NotFound,
    BadRequest
}

public sealed record DatabaseExplorerResult<T>(
    DatabaseExplorerStatus Status,
    T? Value = default,
    string? Detail = null)
{
    public static DatabaseExplorerResult<T> Success(T value) => new(DatabaseExplorerStatus.Success, value);
    public static DatabaseExplorerResult<T> NotFound() => new(DatabaseExplorerStatus.NotFound);
    public static DatabaseExplorerResult<T> BadRequest(string detail) => new(DatabaseExplorerStatus.BadRequest, Detail: detail);
}
