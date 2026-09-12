namespace AgentUp.Tray.Features.Tray;

/// <summary>
/// The wording the tray menu and tooltip show for each service state.
/// </summary>
/// <remarks>
/// Extracted from the menu controller so the presentation rules are testable without an
/// Avalonia application, tray icon, or native menu.
/// </remarks>
public static class TrayStatusText
{
    public static string MenuLine(ServiceState state) => $"Agent-Up  {Word(state)}";

    public static string ToolTip(ServiceState state) => $"Agent-Up · {Word(state)}";

    /// <summary>Restart is offered only once the service is actually reachable.</summary>
    public static bool CanRestart(ServiceState state) => state == ServiceState.Connected;

    public static string Word(ServiceState state)
        => state switch
        {
            ServiceState.Connecting => "Connecting...",
            ServiceState.Connected => "Running",
            ServiceState.Restarting => "Restarting...",
            ServiceState.Disconnected => "Disconnected",
            _ => "Unknown"
        };
}
