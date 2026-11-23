namespace AgentUp.Desktop.Features.Interface.Models;

/// <summary>One repository host, and whether a personal access token is stored for it.</summary>
public sealed record TaskSourceModule(string ModuleId, string DisplayName, bool Connected, string Detail);

/// <summary>The forge connections. The token is never in this document.</summary>
public sealed record TaskSource(IReadOnlyList<TaskSourceModule> Modules);

/// <summary>A repository the stored token can see.</summary>
public sealed record TaskSourceRepository(string ModuleId, string Owner, string Name);

/// <summary>An open pull request and the review standings the Server inferred from it.</summary>
public sealed record TaskSourcePull(string Id, int Number, string Title, string Url, IReadOnlyList<TaskSourceReview> Reviews);

/// <summary>One kind of review. Standing is waiting, reviewing, or reviewed.</summary>
public sealed record TaskSourceReview(string Kind, string Standing, string? Login, string? Verdict);
