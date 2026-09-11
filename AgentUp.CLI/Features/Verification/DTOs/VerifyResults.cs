using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.CLI.Features.Verification.DTOs;

/// <summary>
/// Results carry an optional <c>Error</c> rather than throwing, so the command layer stays
/// at routing complexity and configuration failures are rendered like any other outcome.
/// </summary>
public sealed record VerifyPlanResult(VerificationPlan? Plan, string? Error);

public sealed record VerifyRunResult(IReadOnlyList<CheckOutcome> Outcomes, string? Error);

public sealed record VerifyGuardResult(GuardReport? Report, string? Error);
