using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Tests.Features.Workspaces.Controller;

[TestFixture]
public sealed class WorkspaceDiagnosticsOutputTests
{
    [Test]
    public void WriteDiagnosticsResult_PrintsStateLogsAndDiagnosticContext()
    {
        using var writer = new StringWriter();
        var output = new WorkspaceCommandOutputService(writer);
        var diagnostics = new WorkspaceDiagnosticsDto(
            "workspace-1", "Shop", "Running", "Unhealthy", DateTimeOffset.UtcNow,
            [new ApplicationDiagnosticsDto("web", "Unhealthy", "Unhealthy", ["server started"], false)],
            [new DiagnosticEntryDto("event-1", DateTimeOffset.UtcNow, "javascript", "error", "active", "web",
                "javascript_exception", "web", "browser-2", "boom", new Dictionary<string, string>())]);

        var exitCode = output.WriteDiagnosticsResult(
            WorkspaceCommandResult<WorkspaceDiagnosticsDto>.Success(diagnostics));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(writer.ToString(), Does.Contain("Diagnostics: Shop"));
            Assert.That(writer.ToString(), Does.Contain("Application: web (Unhealthy, Unhealthy)"));
            Assert.That(writer.ToString(), Does.Contain("log: server started"));
            Assert.That(writer.ToString(), Does.Contain("[active] javascript/error app=web browser=browser-2: boom"));
        });
    }
}
