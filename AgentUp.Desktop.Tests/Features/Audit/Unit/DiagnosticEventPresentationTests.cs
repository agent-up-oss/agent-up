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

    [Test]
    public void Present_UnhealthyHealthEvent_UsesRedCategory()
    {
        var presentation = DiagnosticEventPresentation.Present(Create(
            "health",
            "port_health_check",
            "unhealthy",
            new Dictionary<string, string>
            {
                ["appName"] = "api",
                ["port"] = "8080",
                ["state"] = "Unhealthy"
            }));

        Assert.That(presentation.CategoryColor, Is.EqualTo(DiagnosticEventPresentation.ErrorColor));
    }

    [Test]
    public void Present_WarningOutcome_UsesWarningColor()
    {
        var presentation = DiagnosticEventPresentation.Present(Create(
            "frontend",
            "load_warning",
            "warning",
            new Dictionary<string, string> { ["message"] = "Slow response" }));

        Assert.That(presentation.CategoryColor, Is.EqualTo(DiagnosticEventPresentation.WarningColor));
    }

    [Test]
    public void Present_StdoutWarningMessage_UsesWarningColor()
    {
        var presentation = DiagnosticEventPresentation.Present(Create(
            "application",
            "application_console_line",
            "success",
            new Dictionary<string, string>
            {
                ["stream"] = "stdout",
                ["message"] = "[WARN] Retrying connection"
            }));

        Assert.Multiple(() =>
        {
            Assert.That(presentation.Category, Is.EqualTo("Stdout"));
            Assert.That(presentation.MessageColor, Is.EqualTo(DiagnosticEventPresentation.WarningColor));
        });
    }

    [Test]
    public void Present_UnknownKind_UsesCapitalizedCategoryLabel()
    {
        var presentation = DiagnosticEventPresentation.Present(Create(
            "customkind",
            "custom_action",
            "success",
            new Dictionary<string, string> { ["message"] = "done" }));

        Assert.That(presentation.Category, Is.EqualTo("Customkind"));
    }

    [Test]
    public void Present_BuildsMessageFromVisibleDetailsWhenMessageMissing()
    {
        var presentation = DiagnosticEventPresentation.Present(Create(
            "browser",
            "navigation_failed",
            "failure",
            new Dictionary<string, string>
            {
                ["url"] = "/checkout",
                ["statusCode"] = "500"
            }));

        Assert.That(presentation.Message, Does.Contain("url: /checkout"));
    }

    private static ApplicationAuditEventDto Create(
        string kind,
        string action,
        string outcome,
        IReadOnlyDictionary<string, string> details)
        => new("evt-1", DateTimeOffset.Parse("2026-08-22T12:00:00Z"), kind, action, outcome, details);
}
