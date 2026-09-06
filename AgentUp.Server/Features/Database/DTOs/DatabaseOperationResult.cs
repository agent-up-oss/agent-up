namespace AgentUp.Server.Features.Database.DTOs;

public sealed record DatabaseOperationResult<T>(bool Found, T? Value, string? Error)
{
    public static DatabaseOperationResult<T> Success(T value) => new(true, value, null);
    public static DatabaseOperationResult<T> NotFound() => new(false, default, null);
    public static DatabaseOperationResult<T> Failed(string error) => new(true, default, error);
}
