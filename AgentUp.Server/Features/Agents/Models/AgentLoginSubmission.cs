namespace AgentUp.Server.Features.Agents.Models;

/// <summary>What a client sent back to finish a sign-in that is already running.</summary>
public enum AgentLoginSubmissionKind
{
    /// <summary>A code the user copied out of the provider page, for a CLI waiting on stdin.</summary>
    Code,

    /// <summary>A redirect URL the client intercepted, to replay against the CLI's listener.</summary>
    Callback
}

public sealed record AgentLoginSubmission(AgentLoginSubmissionKind Kind, string Value);
