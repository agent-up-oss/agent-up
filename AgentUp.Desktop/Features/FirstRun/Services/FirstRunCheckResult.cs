namespace AgentUp.Desktop.Features.FirstRun.Services;

public sealed record FirstRunCheckResult(bool IsSuccess, string Message)
{
    public static FirstRunCheckResult Success(string message) => new(true, message);

    public static FirstRunCheckResult Failure(string message) => new(false, message);
}
