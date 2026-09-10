using AgentUp.Desktop.Features.Audit.DTOs;
using AgentUp.Desktop.Features.Audit.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Audit.Unit;

[TestFixture]
public sealed class DiagnosticEventPresentationTests
{
    [Test]
    public void Present_HealthHealthy_UsesGreenCategory()
    {
        var presentation = DiagnosticEventPresentation.Present(Create(
            "health",
            "port_health_check",
            "healthy",
            new Dictionary<string, string>
            {
                ["appName"] = "api",
                ["port"] = "8080",
                ["state"] = "Healthy",
                ["url"] = "http://127.0.0.1:8080/health"
            }));

        Assert.Multiple(() =>
        {
            Assert.That(presentation.Category, Is.EqualTo("Health"));
            Assert.That(presentation.CategoryColor, Is.EqualTo(DiagnosticEventPresentation.SuccessColor));
        });
    }

    [Test]
    public void Present_StderrLine_UsesRedCategoryAndMessage()
    {
        var presentation = DiagnosticEventPresentation.Present(Create(
            "application",
            "application_console_line",
            "success",
            new Dictionary<string, string>
            {
                ["stream"] = "stderr",
                ["message"] = "1/1 brokers are down"
            }));

        Assert.Multiple(() =>
        {
            Assert.That(presentation.Category, Is.EqualTo("Stderr"));
            Assert.That(presentation.CategoryColor, Is.EqualTo(DiagnosticEventPresentation.ErrorColor));
            Assert.That(presentation.MessageColor, Is.EqualTo(DiagnosticEventPresentation.ErrorColor));
        });
    }

    [Test]
    public void Present_StdoutErrorMessage_UsesRedMessage()
    {
        var presentation = DiagnosticEventPresentation.Present(Create(
            "application",
            "application_console_line",
            "success",
            new Dictionary<string, string>
            {
                ["stream"] = "stdout",
                ["message"] = "[ERROR] Kafka connection failed"
            }));

        Assert.Multiple(() =>
        {
            Assert.That(presentation.Category, Is.EqualTo("Stdout"));
            Assert.That(presentation.MessageColor, Is.EqualTo(DiagnosticEventPresentation.ErrorColor));
        });
    }

    [Test]
    public void Present_FrontendFailure_UsesRedCategory()
    {
        var presentation = DiagnosticEventPresentation.Present(Create(
            "frontend",
            "load_failed",
            "failure",
            new Dictionary<string, string> { ["message"] = "Load failed" }));

        Assert.That(presentation.CategoryColor, Is.EqualTo(DiagnosticEventPresentation.ErrorColor));
    }

    private static ApplicationAuditEventDto Create(
        string kind,
        string action,
        string outcome,
        IReadOnlyDictionary<string, string> details)
        => new("evt-1", DateTimeOffset.Parse("2026-08-22T12:00:00Z"), kind, action, outcome, details);
}
