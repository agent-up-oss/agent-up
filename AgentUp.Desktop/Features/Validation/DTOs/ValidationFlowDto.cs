namespace AgentUp.Desktop.Features.Validation.DTOs;

public enum ValidationActionDto { Navigate, Click, Fill, Press }
public enum ValidationExpectationDto { Url, Title, Text, Visible }

public sealed record ValidationTargetDto(
    string? Role = null,
    string? Name = null,
    string? Label = null,
    string? TestId = null,
    string? Text = null,
    string? Selector = null);

public sealed record ValidationAssertionDto(
    ValidationExpectationDto Kind,
    string Value,
    ValidationTargetDto? Target = null);

public sealed record ValidationStepDto(
    string Id,
    string Description,
    ValidationActionDto Action,
    ValidationTargetDto? Target = null,
    string? Value = null,
    IReadOnlyList<ValidationAssertionDto>? Expectations = null);

public sealed record ValidationFlowDto(
    string Id,
    string WorkspaceId,
    string Application,
    string Name,
    string Description,
    string InitialPath,
    IReadOnlyList<ValidationAssertionDto> InitialExpectations,
    IReadOnlyList<ValidationStepDto> Steps,
    int Version);
