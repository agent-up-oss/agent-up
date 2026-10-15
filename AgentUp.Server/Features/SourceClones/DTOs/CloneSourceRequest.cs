namespace AgentUp.Server.Features.SourceClones.DTOs;

// Repository and Branch are nullable so the slice owns the validation messages instead of the
// framework's implicit required-field errors for non-nullable reference types. Revision is the
// named Git revision for a derived review workspace; when it is omitted, Branch is the revision.
public sealed record CloneSourceRequest(string? Repository, string? Branch)
{
    public string? Revision { get; init; }

    public bool Derived { get; init; }
}
