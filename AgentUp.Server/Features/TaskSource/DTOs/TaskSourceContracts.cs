namespace AgentUp.Server.Features.TaskSource.DTOs;

/// <summary>
/// The forge connections a client shows. Each identity is one team member. The token is never in
/// this document: a client can see that a member is connected, and nothing it could replay.
/// </summary>
public sealed record TaskSourceDto(IReadOnlyList<TaskSourceIdentityDto> Identities);

/// <summary>
/// One team member, whether a personal access token is stored for them, which host that token
/// talks to, and the origin. The token itself is never here.
/// </summary>
public sealed record TaskSourceIdentityDto(
    string WorkspaceId,
    string DisplayName,
    string Initials,
    string? ModuleId,
    bool Connected,
    string Detail,
    string? EndpointUrl);

/// <summary>
/// A personal access token and the host URL it should use, saved for one team member. Both are
/// checked, then stored. The token is never returned.
/// </summary>
public sealed record SaveTaskSourceTokenRequest(string WorkspaceId, string ModuleId, string EndpointUrl, string Token);

/// <summary>A team member a client can draw as a round banner.</summary>
public sealed record TaskSourceMemberDto(string WorkspaceId, string DisplayName, string Initials);

/// <summary>
/// A repository any stored token can see, and the team members whose tokens can see it.
/// </summary>
public sealed record TaskSourceRepositoryDto(
    string ModuleId,
    string Owner,
    string Name,
    IReadOnlyList<TaskSourceMemberDto>? Members = null);

/// <summary>An open pull request and the review standings inferred from it.</summary>
public sealed record TaskSourcePullDto(
    string Id,
    int Number,
    string Title,
    string Url,
    IReadOnlyList<TaskSourceReviewDto> Reviews,
    string? HeadRef = null,
    string? BaseRef = null,
    string? HeadSha = null,
    string? Body = null,
    string? HeadOwner = null);

/// <summary>
/// One kind of review a pull request still needs or has already received.
/// <paramref name="Standing"/> is <c>waiting</c>, <c>reviewing</c>, or <c>reviewed</c>.
/// <paramref name="CommitId"/> is the host commit that standing was left on, so a later push
/// can reopen the kind.
/// </summary>
public sealed record TaskSourceReviewDto(
    string Kind,
    string Standing,
    string? Login,
    string? Verdict,
    string? CommitId = null);
