using AgentUp.Sdk.Common;
using AgentUp.Sdk.Forge;

namespace AgentUp.Server.Tests.Support;

internal sealed class StubForgeCapability : IForgeCapability
{
    public CapabilityIdentity Identity { get; init; } = new("github", "1.0.0", "GitHub", "agent-up");

    public string Kind => CapabilityKind.Forge;

    public IReadOnlyList<CapabilitySoftware> Software { get; } = [];

    public IReadOnlyList<CapabilitySettingSpec> Settings { get; } = [];

    public ForgeHostConfiguration Host { get; init; } = new(
        "https://example.test/api/",
        "https://example.test/login/oauth/authorize",
        "https://example.test/login/oauth/access_token");

    public ForgeHostSnapshot Snapshot { get; set; } = new(null, [], [], null);

    /// <summary>The account each credential acts as, by token; a token not listed has no account.</summary>
    public Dictionary<string, string> Accounts { get; } = new(StringComparer.Ordinal);

    public List<string> ReadBranches { get; } = [];

    public IReadOnlyList<ForgeIssue> Assigned { get; init; } = [];

    public IReadOnlyList<ForgeIssue> Issues { get; init; } = [];

    public Exception? ReadFailure { get; init; }

    public Exception? ListAssignedFailure { get; init; }

    public Exception? ListIssuesFailure { get; init; }

    public Exception? UpdateRefFailure { get; init; }

    public ForgeProposal? Opened { get; init; }

    public int UpdateRefCalls { get; private set; }

    public int CommentCalls { get; private set; }

    public int ReviewCalls { get; private set; }

    public string? LastReadToken { get; private set; }

    public Task<ForgeHostSnapshot> ReadAsync(ForgeRequest request, CancellationToken cancellationToken = default)
    {
        LastReadToken = request.Token;
        ReadBranches.Add(request.Branch);
        var snapshot = Accounts.TryGetValue(request.Token, out var login)
            ? Snapshot with { Account = new ForgeAccount(login, login, false, $"https://example.test/{login}") }
            : Snapshot;
        return ReadFailure is not null
            ? Task.FromException<ForgeHostSnapshot>(ReadFailure)
            : Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<ForgeIssue>> ListAssignedAsync(
        ForgeRequest request,
        CancellationToken cancellationToken = default)
        => ListAssignedFailure is not null
            ? Task.FromException<IReadOnlyList<ForgeIssue>>(ListAssignedFailure)
            : Task.FromResult(Assigned);

    public Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(
        ForgeRequest request,
        CancellationToken cancellationToken = default)
        => ListIssuesFailure is not null
            ? Task.FromException<IReadOnlyList<ForgeIssue>>(ListIssuesFailure)
            : Task.FromResult(Issues);

    public Task<ForgeProposal> OpenProposalAsync(
        ForgeRequest request,
        ForgeOpenProposalCommand command,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Opened ?? new ForgeProposal("1", "42", command.Title, "https://example.test/42", command.HeadRef, command.BaseRef, "open"));

    public Task UpdateRefAsync(ForgeRequest request, ForgeUpdateRefCommand command, CancellationToken cancellationToken = default)
    {
        if (UpdateRefFailure is not null)
            return Task.FromException(UpdateRefFailure);
        UpdateRefCalls++;
        return Task.CompletedTask;
    }

    public Task CommentAsync(ForgeRequest request, ForgeCommentCommand command, CancellationToken cancellationToken = default)
    {
        CommentCalls++;
        return Task.CompletedTask;
    }

    public Task SubmitReviewAsync(ForgeRequest request, ForgeSubmitReviewCommand command, CancellationToken cancellationToken = default)
    {
        ReviewCalls++;
        LastReview = command;
        return Task.CompletedTask;
    }

    public ForgeSubmitReviewCommand? LastReview { get; private set; }

    /// <summary>Every write that names a team member, as "token: issue/proposal -> login".</summary>
    public List<string> TeamWrites { get; } = [];

    public ForgeAttachment? Attachment { get; set; }

    public Task AssignIssueAsync(ForgeRequest request, int issueNumber, string login, CancellationToken cancellationToken = default)
    {
        TeamWrites.Add($"{request.Token}: assign #{issueNumber} -> {login}");
        return Task.CompletedTask;
    }

    public Task RequestReviewAsync(ForgeRequest request, string proposalNumber, string login, CancellationToken cancellationToken = default)
    {
        TeamWrites.Add($"{request.Token}: review !{proposalNumber} -> {login}");
        return Task.CompletedTask;
    }

    public Task<ForgeAttachment?> AttachAsync(ForgeRequest request, string issueNumber, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default)
    {
        TeamWrites.Add($"{request.Token}: attach {fileName} to {issueNumber}");
        return Task.FromResult(Attachment);
    }
}
