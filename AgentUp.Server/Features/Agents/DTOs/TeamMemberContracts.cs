using System.ComponentModel.DataAnnotations;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.DTOs;

public sealed record TeamMemberRequest(
    [Required] TeamMemberKind Kind,
    string? SystemPrompt = null,
    string? CustomizationPrompt = null);

public sealed record TeamMemberDto(
    TeamMemberKind Kind,
    bool ReadsRepository,
    bool WritesRepository,
    bool ControlsBrowser,
    bool SubmitsReviews,
    AgentFixtureKind Fixture,
    IReadOnlyList<string> OperationPermissions,
    string SystemPrompt = "",
    string? CustomizationPrompt = null,
    string DefaultSystemPrompt = "");
