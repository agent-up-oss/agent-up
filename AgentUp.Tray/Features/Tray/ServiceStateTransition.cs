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
    /// <remarks>
    /// A reachable server means Connected from any state, Restarting included. Holding
    /// Restarting until a poll fails - which the loop this replaced did - leaves the tray
    /// stuck whenever a restart is quick enough that no poll ever fails: the label stays
    /// "Restarting..." and, because restart is only offered while Connected, the menu item
    /// stays disabled for the life of the process. A brief "Running" while the old server
    /// answers its last poll is the cheaper wrong answer, and it corrects itself.
    /// </remarks>
    public static ServiceState? Next(ServiceState current, bool pollSucceeded)
    {
        if (pollSucceeded)
            return ServiceState.Connected;

        if (current is ServiceState.Connected or ServiceState.Restarting)
            return ServiceState.Disconnected;

        return null;
    }

    /// <summary>Whether a heartbeat should be sent in this state.</summary>
    public static bool ShouldHeartbeat(ServiceState current) => current == ServiceState.Connected;
}
