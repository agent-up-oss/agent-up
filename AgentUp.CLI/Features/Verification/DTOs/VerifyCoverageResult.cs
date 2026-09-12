using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.CLI.Features.Verification.DTOs;

/// <summary>
/// Carries an optional error rather than throwing, so the command layer stays at routing
/// complexity like the other verify subcommands.
/// </summary>
public sealed record VerifyCoverageResult(PatchCoverageResult? Coverage, string? Error);
