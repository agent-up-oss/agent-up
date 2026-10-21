using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>Writes report.json, report.html, and report.png into a directory the caller names.</summary>
public interface IRunReportWriter
{
    Task WriteAsync(string directory, RunReport report, CancellationToken cancellationToken);
}
