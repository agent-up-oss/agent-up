namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// The PNG could not be rendered. JSON and HTML are already on disk; the run's exit code
/// stays whatever the checks decided.
/// </summary>
public sealed class ReportCaptureException(string message) : Exception(message);
