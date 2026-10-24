namespace AgentUp.Desktop.Features.Validation.Models;

public sealed record DesktopExpectationSnapshot(bool Passed, string? Actual, string? Error = null);
