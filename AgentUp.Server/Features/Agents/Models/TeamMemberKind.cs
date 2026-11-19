namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// What a workspace is on its Server's team. One Server hosts many workspaces; each is one team
/// member with its own checkout and its own orchestration. The kind decides what that member's
/// agent may touch, not which agent it runs.
/// </summary>
public enum TeamMemberKind
{
    /// <summary>Writes code. Reads and writes the repository; addresses review threads.</summary>
    Author,

    /// <summary>Reviews code. Reads the repository, never writes it, never drives the browser.</summary>
    DevReviewer,

    /// <summary>Reviews the product. Never reads the repository; drives the browser.</summary>
    PmReviewer
}
