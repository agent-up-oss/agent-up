namespace AgentUp.Server.Features.Files.Models;

public sealed record WorkspaceFileResult<T>(WorkspaceFileOutcome Outcome, T? Value, string? Error) where T : class
{
    public static WorkspaceFileResult<T> Success(T value) => new(WorkspaceFileOutcome.Succeeded, value, null);

    public static WorkspaceFileResult<T> Failed(WorkspaceFileOutcome outcome, string error) => new(outcome, null, error);

    /// <summary>The HTTP status a failed outcome answers with.</summary>
    public int StatusCode => Outcome switch
    {
        WorkspaceFileOutcome.Succeeded => 200,
        WorkspaceFileOutcome.WorkspaceNotFound or WorkspaceFileOutcome.FileNotFound => 404,
        WorkspaceFileOutcome.Stale or WorkspaceFileOutcome.Busy => 409,
        _ => 400,
    };

    /// <summary>The problem title a failed outcome answers with, so a client can tell a stale save from a busy agent.</summary>
    public string Title => Outcome switch
    {
        WorkspaceFileOutcome.WorkspaceNotFound => "Workspace not found",
        WorkspaceFileOutcome.FileNotFound => "Not found",
        WorkspaceFileOutcome.Stale => "File changed on the Server",
        WorkspaceFileOutcome.Busy => "Workspace agent is working",
        _ => "Invalid file request",
    };
}
