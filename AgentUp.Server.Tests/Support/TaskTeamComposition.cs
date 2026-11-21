using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Git.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// The services around a two-member team - an author, bob, and a dev reviewer, lucy - on one
/// repository, with Git and the host faked. It composes services only: which tasks are queued is
/// each test's to arrange.
/// </summary>
internal static class TaskTeamComposition
{
    public static async Task<TaskTeam> ComposeAsync(string? startError)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var bob = (await workspaces.RegisterAsync(ServerDomain.Workspace().Build())).Id;
        var lucy = (await workspaces.RegisterAsync(ServerDomain.SecondWorkspace().Build())).Id;
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var members = new TeamMemberController(new TeamMemberService(settings, workspaces));
        members.Set(lucy, TeamMemberKind.DevReviewer);
        var forge = new StubForgeCapability();
        forge.Accounts["bob-token"] = "bob";
        forge.Accounts["lucy-token"] = "lucy";
        var credentials = new MemoryForgeCredentialStore();
        credentials.Write(bob, "github", "bob-token");
        credentials.Write(lucy, "github", "lucy-token");
        var transport = new RecordingForgeGitTransport();
        var forgeController = new ForgeController(new ForgeService(
            workspaces,
            new FakeEnabledCapabilityPackages().WithForges(forge),
            credentials,
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://example.test/acme/widgets.git")),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("unused"),
            null,
            members,
            transport));
        var git = new FakeGitWorkingTreeProvider();
        var changes = new GitChangeTreeService(workspaces, git, new FakeWorkspacePromptGuard());
        var queue = new TaskQueueService(new MemoryTaskQueueStore());
        var progress = new MemoryRoleProgress();
        var applications = new FixedReviewApplicationHost(["http://127.0.0.1:5701/"], startError);
        var requirements = new TaskReviewRequirementService(forgeController, workspaces, settings);
        var pipeline = new TaskPipelineService(queue, forgeController, new GitWorkspaceController(changes), new GitQueryController(changes), workspaces, settings, progress, requirements, applications);
        var evidence = new FixedReviewEvidenceSource();
        var work = new TaskWorkController(new TaskWorkService(queue, pipeline, forgeController, settings, evidence));
        return new TaskTeam(pipeline, queue, git, transport, forge, progress, bob, lucy, forgeController, new GitQueryController(changes), members, applications, settings, workspaces, work, evidence);
    }
}

internal sealed record TaskTeam(
    TaskPipelineService Pipeline,
    TaskQueueService Queue,
    FakeGitWorkingTreeProvider Git,
    RecordingForgeGitTransport Transport,
    StubForgeCapability Forge,
    MemoryRoleProgress Progress,
    string Bob,
    string Lucy,
    ForgeController Host,
    GitQueryController GitRead,
    TeamMemberController Members,
    FixedReviewApplicationHost Applications,
    InMemoryAgentWorkspaceSettingsRepository Settings,
    WorkspaceQueryController Workspaces,
    TaskWorkController Work,
    FixedReviewEvidenceSource Evidence);
