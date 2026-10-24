namespace AgentUp.Server.Features.Validation.DTOs;

public enum ValidationAction { Navigate, Click, Fill, Press }
public enum ValidationExpectation { Url, Title, Text, Visible, Running, Enabled, State, Absent }
public enum ValidationSurface { Application = 0, Desktop = 1, Mobile = 2 }

public sealed record ValidationTarget(string? Role = null, string? Name = null, string? Label = null, string? TestId = null, string? Text = null, string? Selector = null, int? X = null, int? Y = null);
public sealed record ValidationAssertion(ValidationExpectation Kind, string Value, ValidationTarget? Target = null);
public sealed record ValidationStep(string Id, string Description, ValidationAction Action, ValidationTarget? Target = null, string? Value = null, IReadOnlyList<ValidationAssertion>? Expectations = null);
public sealed record ValidationFlow(string Id, string WorkspaceId, string Application, string Name, string Description, string InitialPath, IReadOnlyList<ValidationAssertion> InitialExpectations, IReadOnlyList<ValidationStep> Steps, DateTimeOffset UpdatedAtUtc, int Version = 1, ValidationSurface Surface = ValidationSurface.Application);
public sealed record SaveValidationFlowRequest(string? Id, string Application, string Name, string Description, string InitialPath, IReadOnlyList<ValidationAssertion>? InitialExpectations, IReadOnlyList<ValidationStep> Steps, ValidationSurface Surface = ValidationSurface.Application);
public sealed record SaveValidationFlowResult(bool Succeeded, ValidationFlow? Flow = null, string? Error = null);
public sealed record ValidationRunResult(bool Succeeded, string Message, string? StepId = null);
public sealed record PlaywrightExport(string FileName, string Content);

public sealed record ValidationTargetResolution(string? ControlType, string? AutomationId, string? Name, double? X, double? Y, double? Width, double? Height);
public sealed record ValidationExpectationResult(ValidationExpectation Kind, string Value, bool Passed, string? Actual = null, string? Message = null);
public sealed record ValidationStepArtifact(
    string Id,
    string Description,
    string Action,
    ValidationTarget? Target,
    ValidationTargetResolution? Resolved,
    IReadOnlyList<ValidationExpectationResult> Expectations,
    bool Passed,
    double DurationMs,
    string Screenshot,
    IReadOnlyList<string>? Candidates = null);
public sealed record ValidationRunReport(
    string FlowId,
    string Surface,
    bool Passed,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    IReadOnlyList<ValidationStepArtifact> Steps,
    string? Error = null);
