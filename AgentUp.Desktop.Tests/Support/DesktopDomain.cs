using AgentUp.Desktop.Features.Workspaces.DTOs;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// The shared domain vocabulary for Desktop tests: one realistic workspace, the
/// applications it runs and the ports they hold, named once and reused everywhere.
/// </summary>
/// <remarks>
/// Tests refer to these names rather than inventing their own id, path and port values, so
/// a reader can tell at a glance whether two tests are talking about the same workspace or
/// different ones. Where a test needs one value to be specific, it injects that value
/// through a builder instead of restating the whole world.
/// </remarks>
internal static class DesktopDomain
{
    public const string WorkspaceId = "ws-1";
    public const string WorkspaceName = "My App";
    public const string RepositoryPath = "/repo/my-app";
    public const string WorktreePath = "/worktrees/my-app";
    public const string Branch = "feat/dashboard";
    public const string Commit = "abc123";
    public const string RunningState = "Running";
    public const string StoppedState = "Stopped";

    public const string SecondWorkspaceId = "ws-2";
    public const string SecondWorkspaceName = "Auth Service";
    public const string SecondRepositoryPath = "/repo/auth";
    public const string SecondWorktreePath = "/worktrees/auth";
    public const string SecondBranch = "fix/token-refresh";
    public const string SecondCommit = "def456";

    public const string ThirdWorkspaceId = "ws-3";
    public const string ThirdWorkspaceName = "API Gateway";
    public const string ThirdRepositoryPath = "/repo/api";
    public const string ThirdWorktreePath = "/worktrees/api";
    public const string ThirdBranch = "main";
    public const string ThirdCommit = "ghi789";

    public const string ApiName = "API";
    public const string ApiCommand = "cargo run";
    public const string DocsName = "Docs";
    public const string DocsCommand = "npm run start";
    public const string DocsPath = "docs";
    public const string ServingName = "App";
    public const string ServingCommand = "cmd";
    public const string ExampleApiName = "Example API";
    public const string DesktopName = "Sample Desktop";
    public const string DesktopCommand = "dotnet run";
    public const string DesktopKind = "Desktop";
    public const string ProcessKind = "Process";

    public const int HttpPort = 3000;

    /// <summary>The canonical workspace: one running worktree with no applications.</summary>
    public static WorkspaceDtoBuilder Workspace() => new();

    /// <summary>A second workspace, so "two different workspaces" has one spelling.</summary>
    public static WorkspaceDtoBuilder SecondWorkspace()
        => new WorkspaceDtoBuilder()
            .WithId(SecondWorkspaceId)
            .Named(SecondWorkspaceName)
            .WithRepositoryPath(SecondRepositoryPath)
            .WithWorktreePath(SecondWorktreePath)
            .OnBranch(SecondBranch)
            .AtCommit(SecondCommit)
            .InState(StoppedState);

    /// <summary>A third workspace, for the ordering and multi-selection cases.</summary>
    public static WorkspaceDtoBuilder ThirdWorkspace()
        => new WorkspaceDtoBuilder()
            .WithId(ThirdWorkspaceId)
            .Named(ThirdWorkspaceName)
            .WithRepositoryPath(ThirdRepositoryPath)
            .WithWorktreePath(ThirdWorktreePath)
            .OnBranch(ThirdBranch)
            .AtCommit(ThirdCommit);

    /// <summary>The canonical set of workspaces a populated sidebar shows.</summary>
    public static List<WorkspaceDto> Workspaces() =>
    [
        Workspace().Build(),
        SecondWorkspace().Build(),
        ThirdWorkspace().Build()
    ];

    /// <summary>The canonical application: a running process with no allocated ports.</summary>
    public static ApplicationDtoBuilder Application() => new(ApiName, ApiCommand);

    /// <summary>A second application, so "two different applications" has one spelling.</summary>
    public static ApplicationDtoBuilder DocsApplication()
        => new ApplicationDtoBuilder(DocsName, DocsCommand).At(DocsPath);

    /// <summary>The canonical hosted desktop application.</summary>
    public static ApplicationDtoBuilder DesktopApplication()
        => new ApplicationDtoBuilder(DesktopName, DesktopCommand).OfKind(DesktopKind);

    /// <summary>The canonical allocated port: an HTTP port mapped to itself.</summary>
    public static PortMappingDtoBuilder Port() => new();

    /// <summary>
    /// The canonical workspace running one application on an allocated HTTP port - the
    /// shape the browser, console and application panels are all tested against.
    /// </summary>
    /// <remarks>
    /// This returns a builder rather than a finished workspace, so "the same workspace but
    /// stopped" is <c>.InState(StoppedState)</c> at the call site.
    /// </remarks>
    public static WorkspaceDtoBuilder WorkspaceServing(int port = HttpPort, string id = WorkspaceId)
        => new WorkspaceDtoBuilder()
            .Identified(id)
            .OnBranch(ThirdBranch)
            .WithApplication(new ApplicationDtoBuilder(ServingName, ServingCommand).WithPort(port));

    /// <summary>
    /// The canonical workspace running both a web application on a port and a hosted
    /// desktop application, which the desktop tab and viewer tests need side by side.
    /// </summary>
    public static WorkspaceDtoBuilder WorkspaceServingWithDesktop(
        int port = HttpPort,
        string desktopState = RunningState,
        string id = WorkspaceId)
        => new WorkspaceDtoBuilder()
            .Identified(id)
            .OnBranch(ThirdBranch)
            .WithApplication(new ApplicationDtoBuilder(ExampleApiName, ServingCommand).WithPort(port))
            .WithApplication(DesktopApplication().InState(desktopState));

    /// <summary>The canonical workspace with two running applications and no ports.</summary>
    public static WorkspaceDtoBuilder WorkspaceWithApplications()
        => Workspace()
            .WithApplication(Application())
            .WithApplication(DocsApplication());

    /// <summary>Console output keyed the way the Desktop output panel reads it.</summary>
    public static Dictionary<string, List<string>> OutputFor(
        string workspaceId, string appName, List<string> lines) =>
        new() { [$"{workspaceId}/{appName}"] = lines };
}
