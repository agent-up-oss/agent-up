using System.Text.Json.Serialization;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>What one reviewer said about one revision of a task's proposal, and which kind of review it was.</summary>
public sealed record TaskReview(string WorkspaceId, TaskReviewVerdict Verdict, int Revision, string Body, TeamMemberKind Kind = TeamMemberKind.DevReviewer);

[JsonConverter(typeof(JsonStringEnumConverter<TaskReviewVerdict>))]
public enum TaskReviewVerdict
{
    Approve,
    RequestChanges,
    Comment
}
