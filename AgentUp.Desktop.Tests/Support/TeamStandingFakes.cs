using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Interfaces;
using AgentUp.Desktop.Features.Agents.Services;
using AgentUp.Desktop.Features.Git.Controllers;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Interfaces;
using AgentUp.Desktop.Features.Git.Services;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Services;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// The two reads the bridge composes, answered from a dictionary. Everything the bridge shows comes
/// from these, so a test states the Server's answer and nothing else.
/// </summary>
internal sealed class StandingAgentFake : IAgentApiProvider
{
    public Dictionary<string, AgentSessionDto?> Sessions { get; } = [];

    public Dictionary<string, Exception> Failures { get; } = [];

    public Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken)
        => Failures.TryGetValue(workspaceId, out var failure)
            ? Task.FromException<AgentSessionDto?>(failure)
            : Task.FromResult(Sessions.GetValueOrDefault(workspaceId));

    public Task<AgentSessionDto?> BindAsync(string workspaceId, string agent, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<AgentSessionDto?> ResumeAsync(string workspaceId, string sessionId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task SendAsync(string workspaceId, string message, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task AuthenticateAsync(string workspaceId, string methodId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task SubmitLoginCodeAsync(string workspaceId, string code, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task DecideAsync(string workspaceId, string requestId, string optionId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task StopAsync(string workspaceId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public IAsyncEnumerable<AgentEventDto> EventsAsync(string workspaceId, long after, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

internal sealed class StandingForgeFake : IGitApiProvider
{
    public Dictionary<string, ForgeHostingDto?> Hosting { get; } = [];

    public Dictionary<string, Exception> Failures { get; } = [];

    public Task<ForgeHostingDto?> GetHostingAsync(string workspaceId, CancellationToken cancellationToken = default)
        => Failures.TryGetValue(workspaceId, out var failure)
            ? Task.FromException<ForgeHostingDto?>(failure)
            : Task.FromResult(Hosting.GetValueOrDefault(workspaceId));

    public Task<GitChangeTreeDto?> GetChangesAsync(string workspaceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<CommitQueueDto?> GetCommitQueueAsync(string workspaceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GitFileDiffDto?> GetFileDiffAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GitCommitResultDto> CommitAsync(string workspaceId, GitCommitRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GitMutationResultDto> DiscardAsync(string workspaceId, GitFilesRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GitMutationResultDto> SwitchBranchAsync(string workspaceId, GitBranchRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GitMutationResultDto> CheckoutRemoteAsync(string workspaceId, GitCheckoutRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GitSyncResultDto> FetchAsync(string workspaceId, GitFetchRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GitSyncResultDto> PullAsync(string workspaceId, GitPullRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GitSyncResultDto> PushAsync(string workspaceId, GitPushRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GitLogDto?> GetLogAsync(string workspaceId, int max = 100, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal static class TeamStandingComposition
{
    public static TeamStandingController Controller(StandingAgentFake agents, StandingForgeFake forge)
        => new(new TeamStandingService(
            new AgentsController(new AgentChatService(agents)),
            new GitController(new GitChangeListService(forge))));

    /// <summary>A loop status with only the fields a test cares about set.</summary>
    public static AgentSessionDto Session(
        string workspaceId,
        string? agent = "Claude",
        string state = "ready",
        AgentLoopStatusDto? loop = null)
        => new(workspaceId, agent, state, null, null, [], null, Loop: loop);

    public static AgentLoopStatusDto Loop(
        bool active,
        string phase,
        int? taskNumber = null,
        string? taskTitle = null,
        string? terminal = null,
        string? stopReason = null,
        string? blockReason = null,
        IReadOnlyList<string>? permissionRuleIds = null)
        => new(active, phase, taskNumber?.ToString(), taskNumber, taskTitle, terminal, stopReason, blockReason, permissionRuleIds);

    public static ForgeHostingDto Hosting(
        string availability = "ready",
        string? number = "44",
        string? title = "Restock featured products",
        IReadOnlyList<ForgeCheckDto>? checks = null,
        IReadOnlyList<ForgeReviewThreadDto>? threads = null,
        string? mergeState = "clean",
        string? detail = null)
        => new(
            availability,
            "github",
            number is null ? null : new ForgeProposalDto("PR", number, title ?? string.Empty, "https://example/pull/" + number, "head", "main", "open"),
            checks ?? [],
            threads ?? [],
            mergeState is null ? null : new ForgeMergeabilityDto(true, mergeState),
            detail);

    public static ForgeCheckDto Check(string name, string status, string conclusion)
        => new(name, status, conclusion, "https://example/checks");

    public static ForgeReviewThreadDto Thread(string id, bool resolved)
        => new(id, "https://example/thread/" + id, id, resolved);
}
