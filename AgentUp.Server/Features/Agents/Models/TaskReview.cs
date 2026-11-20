using System.Text.Json.Serialization;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>What one reviewer said about one revision of a task's proposal.</summary>
public sealed record TaskReview(string WorkspaceId, TaskReviewVerdict Verdict, int Revision, string Body);

[JsonConverter(typeof(JsonStringEnumConverter<TaskReviewVerdict>))]
public enum TaskReviewVerdict
{
    Approve,
    RequestChanges,
    Comment
}
