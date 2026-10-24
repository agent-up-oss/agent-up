namespace AgentUp.Server.Features.Workspaces.Models;

internal static class ApplicationReadiness
{
    public static bool IsReady(
        ApplicationReadinessSignal signal,
        bool processAlive,
        bool tcpReady,
        bool httpReady,
        bool settled)
    {
        if (!processAlive)
            return false;

        return signal.Kind switch
        {
            ApplicationReadinessKind.HealthCheck => httpReady,
            ApplicationReadinessKind.TcpPort => tcpReady,
            ApplicationReadinessKind.Container => true,
            _ => settled
        };
    }

    public static bool HasSettled(DateTimeOffset startedAt, DateTimeOffset now, TimeSpan settleWindow)
        => now - startedAt >= settleWindow;

    public static string DiagnosticsSignal(string? lastError, ApplicationReadinessSignal signal)
        => string.IsNullOrWhiteSpace(lastError) ? signal.Description : lastError;
}
