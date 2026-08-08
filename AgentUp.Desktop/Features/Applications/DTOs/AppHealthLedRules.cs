namespace AgentUp.Desktop.Features.Applications.DTOs;

public static class AppHealthLedRules
{
    public static string StateColor(string? state) => state switch
    {
        "Healthy" or "Running" => "#00d66b",
        "Checking"             => "#e8a832",
        "Unhealthy" or "Failed"=> "#b85a5a",
        _                      => "#5a5a72"
    };
}

public enum PortLedState { Probing, Checking, Healthy, Unhealthy }
