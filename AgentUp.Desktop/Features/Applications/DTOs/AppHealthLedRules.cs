using AgentUp.Desktop.Shared.Models;
namespace AgentUp.Desktop.Features.Applications.DTOs;

public static class AppHealthLedRules
{
    public static string StateColor(string? state) => state switch
    {
        "Healthy" or "Running" => AgentUpThemeColors.StatusHealthy,
        "Checking"              => AgentUpThemeColors.StatusWarning,
        "Unhealthy" or "Failed" => AgentUpThemeColors.StatusDanger,
        _                         => AgentUpThemeColors.TextMuted
    };
}

public enum PortLedState { Probing, Checking, Healthy, Unhealthy }
