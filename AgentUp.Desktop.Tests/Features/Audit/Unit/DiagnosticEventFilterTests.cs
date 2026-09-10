using AgentUp.Desktop.Features.Audit.DTOs;
using AgentUp.Desktop.Features.Audit.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Audit.Unit;

[TestFixture]
public sealed class DiagnosticEventFilterTests
{
    [Test]
    public void MatchesSearch_FindsTextInMessageAndDetails()
    {
        var dto = new ApplicationAuditEventDto(
            "e1",
            DateTimeOffset.Parse("2026-08-22T12:00:00Z"),
            "application",
            "application_console_line",
            "success",
            new Dictionary<string, string>
            {
                ["stream"] = "stderr",
                ["message"] = "1/1 brokers are down"
            });

        Assert.Multiple(() =>
        {
            Assert.That(DiagnosticEventFilter.MatchesSearch(dto, "brokers"), Is.True);
            Assert.That(DiagnosticEventFilter.MatchesSearch(dto, "stderr"), Is.True);
            Assert.That(DiagnosticEventFilter.MatchesSearch(dto, "missing"), Is.False);
        });
    }

    [Test]
    public void MatchesCategories_RespectsStdoutAndStderrFilters()
    {
        var filters = new List<DiagnosticKindFilterOption>
        {
            new("Stdout", "application", isSelected: true, stream: "stdout"),
            new("Stderr", "application", isSelected: false, stream: "stderr")
        };
        var stdout = ConsoleLine("stdout");
        var stderr = ConsoleLine("stderr");

        Assert.Multiple(() =>
        {
            Assert.That(DiagnosticEventFilter.MatchesCategories(stdout, filters), Is.True);
            Assert.That(DiagnosticEventFilter.MatchesCategories(stderr, filters), Is.False);
        });
    }

    private static ApplicationAuditEventDto ConsoleLine(string stream)
        => new(
            stream,
            DateTimeOffset.Parse("2026-08-22T12:00:00Z"),
            "application",
            "application_console_line",
            "success",
            new Dictionary<string, string> { ["stream"] = stream, ["message"] = "line" });
}
