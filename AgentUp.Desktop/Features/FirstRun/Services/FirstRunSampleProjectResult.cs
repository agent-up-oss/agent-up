namespace AgentUp.Desktop.Features.FirstRun.Services;

public sealed record FirstRunSampleProjectResult(
    bool IsSuccess,
    string Message,
    string? ProjectDirectory)
{
    public static FirstRunSampleProjectResult Success(string message, string projectDirectory)
        => new(true, message, projectDirectory);

    public static FirstRunSampleProjectResult Failure(string message)
        => new(false, message, null);
}
