using System.ComponentModel.DataAnnotations;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.DTOs;

public sealed record TeamMemberRequest([Required] TeamMemberKind Kind);

public sealed record TeamMemberDto(
    TeamMemberKind Kind,
    bool ReadsRepository,
    bool WritesRepository,
    bool ControlsBrowser,
    bool SubmitsReviews,
    AgentFixtureKind Fixture,
    IReadOnlyList<string> OperationPermissions);
