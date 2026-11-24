namespace AgentUp.Server.Features.TaskSource.DTOs;

/// <summary>
/// The forge connections a client shows. Each module is one host. The token is never in this
/// document: a client can see that a host is connected, and nothing it could replay.
/// </summary>
public sealed record TaskSourceDto(IReadOnlyList<TaskSourceModuleDto> Modules);

/// <summary>
/// One repository host, whether a personal access token is stored for it, and the origin that
/// token talks to. The token itself is never here.
/// </summary>
public sealed record TaskSourceModuleDto(
    string ModuleId,
    string DisplayName,
    bool Connected,
    string Detail,
    string? EndpointUrl);

/// <summary>
/// A personal access token and the host URL it should use. Both are checked, then stored. The
/// token is never returned.
/// </summary>
public sealed record SaveTaskSourceTokenRequest(string ModuleId, string EndpointUrl, string Token);

/// <summary>A repository the stored token can see.</summary>
public sealed record TaskSourceRepositoryDto(string ModuleId, string Owner, string Name);

/// <summary>An open pull request and the review standings inferred from it.</summary>
public sealed record TaskSourcePullDto(
    string Id,
    int Number,
    string Title,
    string Url,
    IReadOnlyList<TaskSourceReviewDto> Reviews);

/// <summary>
/// One kind of review a pull request still needs or has already received.
/// <paramref name="Standing"/> is <c>waiting</c>, <c>reviewing</c>, or <c>reviewed</c>.
/// </summary>
public sealed record TaskSourceReviewDto(string Kind, string Standing, string? Login, string? Verdict);
