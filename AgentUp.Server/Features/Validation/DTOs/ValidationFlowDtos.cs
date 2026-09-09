namespace AgentUp.Server.Features.Validation.DTOs;

public enum ValidationAction { Navigate, Click, Fill, Press }
public enum ValidationExpectation { Url, Title, Text, Visible }

public sealed record ValidationTarget(string? Role = null, string? Name = null, string? Label = null, string? TestId = null, string? Text = null, string? Selector = null);
public sealed record ValidationAssertion(ValidationExpectation Kind, string Value, ValidationTarget? Target = null);
public sealed record ValidationStep(string Id, string Description, ValidationAction Action, ValidationTarget? Target = null, string? Value = null, IReadOnlyList<ValidationAssertion>? Expectations = null);
public sealed record ValidationFlow(string Id, string WorkspaceId, string Application, string Name, string Description, string InitialPath, IReadOnlyList<ValidationAssertion> InitialExpectations, IReadOnlyList<ValidationStep> Steps, DateTimeOffset UpdatedAtUtc, int Version = 1);
public sealed record SaveValidationFlowRequest(string? Id, string Application, string Name, string Description, string InitialPath, IReadOnlyList<ValidationAssertion>? InitialExpectations, IReadOnlyList<ValidationStep> Steps);
public sealed record ValidationRunResult(bool Succeeded, string Message, string? StepId = null);
public sealed record PlaywrightExport(string FileName, string Content);
