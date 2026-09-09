namespace AgentUp.Server.Features.SourceClones.DTOs;

// Repository and Branch are nullable so the slice owns the validation messages instead of the
// framework's implicit required-field errors for non-nullable reference types.
public sealed record CloneSourceRequest(string? Repository, string? Branch);
