namespace AgentUp.Server.Features.SourceClones.DTOs;

// Revision is optional so a refresh with an empty body keeps the revision the workspace
// was created at. A named value moves that same clone onto a different branch, tag, or commit.
public sealed record RefreshSourceRequest(string? Revision);
