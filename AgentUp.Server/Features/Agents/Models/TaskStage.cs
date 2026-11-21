using System.Text.Json.Serialization;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// Where a picked task is between the team members. An author moves it to review by opening a
/// proposal; reviewers move it on by what they say about the revision in front of them.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TaskStage>))]
public enum TaskStage
{
    /// <summary>Picked and waiting for an author.</summary>
    Queued,

    /// <summary>An author holds it and is changing the code.</summary>
    Implementing,

    /// <summary>A proposal is open and the current revision is waiting on reviewers.</summary>
    InReview,

    /// <summary>A reviewer asked for changes to the current revision; it is back with its author.</summary>
    ChangesRequested,

    /// <summary>Every reviewer on the repository accepted the current revision. Merging stays with the host.</summary>
    Approved
}
