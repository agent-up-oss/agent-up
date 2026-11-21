namespace AgentUp.Server.Features.Agents.DTOs;

/// <summary>
/// The task in front of a team member, as its agent reads it through <c>get_task</c>: the issue,
/// where it is, every review of the open revision, and what the host said about it - findings at
/// their lines, check results with where to read their logs, and comments. <paramref name="Next"/>
/// is the one thing this member does with it.
/// </summary>
public sealed record TaskBriefDto(
    string Role,
    int Number,
    string Title,
    string Body,
    string IssueUrl,
    string Stage,
    TaskProposalDto? Proposal,
    IReadOnlyList<TaskReviewDto> Reviews,
    IReadOnlyList<TaskFindingDto> Findings,
    IReadOnlyList<TaskCheckDto> Checks,
    IReadOnlyList<TaskCommentDto> Comments,
    string Next);

/// <summary>A review finding: one comment on one line of one changed file.</summary>
public sealed record TaskFindingDto(string Path, int Line, string Comment, string? Author = null);

/// <summary>One check's latest result on the revision; the URL is where its log is.</summary>
public sealed record TaskCheckDto(string Name, string Status, string Conclusion, string Url);

/// <summary>A comment on the proposal that is not on a line of code.</summary>
public sealed record TaskCommentDto(string? Author, string Body);

/// <summary>What became of a submission: done, with where the result is, or why not.</summary>
public sealed record TaskSubmissionDto(bool Done, string Message, string? Url = null);
