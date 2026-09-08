namespace AgentUp.Server.Features.Git.DTOs;

// Files and Message are nullable so the slice owns the validation messages instead of the
// framework's implicit required-field errors for non-nullable reference types.
public sealed record GitCommitRequest(IReadOnlyList<string>? Files, string? Message);
