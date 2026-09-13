using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Tests.Features.Applications.Unit;

[TestFixture]
public sealed class AppHealthLedRulesTests
{
    [TestCase("Healthy", AgentUpThemeColors.StatusHealthy)]
    [TestCase("Running", AgentUpThemeColors.StatusHealthy)]
    [TestCase("Checking", AgentUpThemeColors.StatusWarning)]
    [TestCase("Unhealthy", AgentUpThemeColors.StatusDanger)]
    [TestCase("Failed", AgentUpThemeColors.StatusDanger)]
    [TestCase("Stopped", AgentUpThemeColors.TextMuted)]
    [TestCase(null, AgentUpThemeColors.TextMuted)]
    public void StateColor_mapsEveryLedState(string? state, string color)
    {
        Assert.That(AppHealthLedRules.StateColor(state), Is.EqualTo(color));
    }
}
