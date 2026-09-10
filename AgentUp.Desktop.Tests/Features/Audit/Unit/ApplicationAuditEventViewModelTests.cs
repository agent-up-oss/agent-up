using AgentUp.Desktop.Features.Audit.DTOs;
using AgentUp.Desktop.Features.Audit.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Audit.Unit;

[TestFixture]
public sealed class ApplicationAuditEventViewModelTests
{
    [Test]
    public void Timestamp_IncludesDateForOlderEntries()
    {
        var yesterday = DateTimeOffset.Now.AddDays(-1);
        var vm = new ApplicationAuditEventViewModel(Create("evt-old", yesterday, "older event"));

        Assert.That(vm.Timestamp, Does.Contain(yesterday.ToString("yyyy-MM-dd")));
    }

    [Test]
    public void Timestamp_UsesTimeOnlyForToday()
    {
        var now = DateTimeOffset.Now;
        var vm = new ApplicationAuditEventViewModel(Create("evt-today", now, "today event"));

        Assert.That(vm.Timestamp, Is.EqualTo(now.ToLocalTime().ToString("HH:mm:ss")));
    }

    private static ApplicationAuditEventDto Create(string eventId, DateTimeOffset timestamp, string message)
        => new(
            eventId,
            timestamp,
            "frontend",
            "load_failed",
            "failure",
            new Dictionary<string, string> { ["message"] = message });
}
