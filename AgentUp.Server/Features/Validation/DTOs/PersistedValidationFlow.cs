namespace AgentUp.Server.Features.Validation.DTOs;

public sealed record PersistedValidationFlow(
    string Id,
    string Application,
    string Name,
    string Description,
    string InitialPath,
    IReadOnlyList<ValidationAssertion> InitialExpectations,
    IReadOnlyList<ValidationStep> Steps,
    DateTimeOffset UpdatedAtUtc,
    int Version = 1);

public sealed record ValidationFlowFile(IReadOnlyList<PersistedValidationFlow> Flows);
