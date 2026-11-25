namespace AgentUp.Desktop.Features.Interface.Models;

/// <summary>
/// One team member, whether a personal access token is stored for them, which host that token
/// talks to, and the origin. The token itself is never here.
/// </summary>
public sealed record TaskSourceIdentity(
    string WorkspaceId,
    string DisplayName,
    string Initials,
    string? ModuleId,
    bool Connected,
    string Detail,
    string? EndpointUrl = null);

/// <summary>The forge connections. The token is never in this document.</summary>
public sealed record TaskSource(IReadOnlyList<TaskSourceIdentity> Identities);

/// <summary>A team member a client can draw as a round banner.</summary>
public sealed record TaskSourceMember(string WorkspaceId, string DisplayName, string Initials)
{
    public static string InitialsFrom(string name)
    {
        var parts = name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}"
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }
}

/// <summary>A repository any stored token can see, and the team members whose tokens can see it.</summary>
public sealed record TaskSourceRepository(
    string ModuleId,
    string Owner,
    string Name,
    IReadOnlyList<TaskSourceMember>? Members = null);

/// <summary>An open pull request and the review standings the Server inferred from it.</summary>
public sealed record TaskSourcePull(string Id, int Number, string Title, string Url, IReadOnlyList<TaskSourceReview> Reviews);

/// <summary>One kind of review. Standing is waiting, reviewing, or reviewed.</summary>
public sealed record TaskSourceReview(string Kind, string Standing, string? Login, string? Verdict);
