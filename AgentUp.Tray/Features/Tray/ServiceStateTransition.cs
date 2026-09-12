namespace AgentUp.Tray.Features.Tray;

/// <summary>
/// Decides the next tray service state from a poll result.
/// </summary>
/// <remarks>
/// Pure and separate from the poll loop so the transition rules can be tested without
/// waiting on real timers or a real server. The loop owns timing; this owns meaning.
/// </remarks>
public static class ServiceStateTransition
{
    /// <summary>
    /// The state to publish, or null when the poll result changes nothing.
    /// </summary>
    public static ServiceState? Next(ServiceState current, bool pollSucceeded)
    {
        // A successful poll during a restart is ignored: the restart is not finished until
        // a later poll, and reporting Connected mid-restart would flicker the menu.
        // Connected is republished on every success, matching the loop this replaced.
        if (pollSucceeded && current != ServiceState.Restarting)
            return ServiceState.Connected;

        if (!pollSucceeded && current is ServiceState.Connected or ServiceState.Restarting)
            return ServiceState.Disconnected;

        return null;
    }

    /// <summary>Whether a heartbeat should be sent in this state.</summary>
    public static bool ShouldHeartbeat(ServiceState current) => current == ServiceState.Connected;
}
