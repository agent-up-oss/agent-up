using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Tests.Support;

/// <summary>
/// The shared domain vocabulary for CLI tests: one realistic workspace and the commits
/// queued against it, named once and reused everywhere.
/// </summary>
/// <remarks>
/// Tests refer to these names rather than inventing their own slice, path and id strings,
/// so a reader can tell at a glance whether two tests are talking about the same commit or
/// different ones. Where a test needs one value to be specific, it injects that value
/// through a builder instead of restating the whole world.
/// </remarks>
internal static class CliDomain
{
    public const string WorkspaceId = "ws-1";
    public const string WorkspaceName = "Primary";
    public const string RepositoryPath = "/repo";
    public const string WorktreePath = "/repo/primary";
    public const string Branch = "main";
    public const string Commit = "c1";

    public const string Slice = "Workspaces";
    public const string SecondSlice = "Commits";
    public const string CommitMessage = "feat(workspaces): register a worktree";
    public const string SecondCommitMessage = "fix(commits): keep the queue ordered";
    public const string CommitFile = "AgentUp.CLI/Features/Workspaces/Services/WorkspaceService.cs";
    public const string SecondCommitFile = "AgentUp.CLI/Features/Commits/Services/CommitsService.cs";

    public const string ApiName = "Api";
    public const string ApiCommand = "dotnet run";

    /// <summary>The canonical workspace as the CLI sees it.</summary>
    public static WorkspaceDtoBuilder Workspace() => new();

    /// <summary>The canonical application declaration.</summary>
    public static ApplicationDefinitionBuilder Application() => new(ApiName, ApiCommand);

    /// <summary>The canonical queued commit.</summary>
    public static CommitEntryBuilder CommitEntry() => new();

    /// <summary>A second queued commit, so "two different commits" has one spelling.</summary>
    public static CommitEntryBuilder SecondCommitEntry()
        => new CommitEntryBuilder()
            .For(SecondSlice)
            .Saying(SecondCommitMessage)
            .Touching(SecondCommitFile);

    /// <summary>The canonical commit queue, holding whichever entries the test is about.</summary>
    public static CommitsQueueBuilder Queue() => new();

    /// <summary>The empty queue every commits test starts from.</summary>
    public static CommitsQueue EmptyQueue() => new CommitsQueueBuilder().Build();
}
