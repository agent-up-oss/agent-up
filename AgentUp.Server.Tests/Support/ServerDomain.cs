using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// The shared domain vocabulary for Server tests: one realistic repository with two
/// worktrees and the applications they run, named once and reused everywhere.
/// </summary>
/// <remarks>
/// Tests refer to these names rather than inventing their own path, branch and commit
/// strings, so a reader can tell at a glance whether two tests are talking about the same
/// workspace or different ones. Where a test needs one value to be specific, it injects
/// that value through a builder instead of restating the whole world.
/// </remarks>
internal static class ServerDomain
{
    public const string RepositoryPath = "/repo";

    public const string WorkspaceName = "Primary";
    public const string WorktreePath = "/repo/primary";
    public const string Branch = "main";
    public const string Commit = "c1";

    public const string SecondWorkspaceName = "Secondary";
    public const string SecondWorktreePath = "/repo/secondary";
    public const string SecondBranch = "feature/secondary";
    public const string SecondCommit = "c2";

    public const string ApiName = "Api";
    public const string ApiCommand = "dotnet run";
    public const string ApiPath = "api";

    public const string WebName = "Web";
    public const string WebCommand = "npm run dev";
    public const string WebPath = "web";

    public const string PortVariable = "PORT";
    public const int DefaultPort = 3000;

    public const string Slice = "Workspaces";
    public const string CommitMessage = "feat(workspaces): register a worktree";
    public const string CommitFile = "AgentUp.Server/Features/Workspaces/Services/WorkspaceRegistry.cs";

    /// <summary>The canonical workspace: the primary worktree of the canonical repository.</summary>
    public static RegisterWorkspaceRequestBuilder Workspace() => new();

    /// <summary>A second workspace on the same repository, so "two different workspaces" has one spelling.</summary>
    public static RegisterWorkspaceRequestBuilder SecondWorkspace()
        => new RegisterWorkspaceRequestBuilder()
            .Named(SecondWorkspaceName)
            .WithWorktreePath(SecondWorktreePath)
            .OnBranch(SecondBranch)
            .AtCommit(SecondCommit);

    /// <summary>The canonical application: an API server started from the worktree root.</summary>
    public static ApplicationDefinitionBuilder Application() => new(ApiName, ApiCommand);

    /// <summary>A second application, so "two different applications" has one spelling.</summary>
    public static ApplicationDefinitionBuilder WebApplication()
        => new ApplicationDefinitionBuilder(WebName, WebCommand).At(WebPath);

    /// <summary>The canonical port declaration: the application's HTTP port.</summary>
    public static PortDeclarationBuilder Port() => new();

    /// <summary>The canonical queued commit.</summary>
    public static CommitEntryBuilder CommitEntry() => new();

    /// <summary>The canonical enqueue request, the controller-boundary form of that commit.</summary>
    public static EnqueueRequestBuilder Enqueue() => new();

    /// <summary>The canonical audit record.</summary>
    public static AuditRecordRequestBuilder AuditRecord() => new();

    /// <summary>The canonical commit queue, holding whichever entries the test is about.</summary>
    public static CommitsQueueBuilder Queue() => new();

    /// <summary>The empty queue every commits test starts from.</summary>
    public static CommitsQueue EmptyQueue() => new CommitsQueueBuilder().Build();
}
