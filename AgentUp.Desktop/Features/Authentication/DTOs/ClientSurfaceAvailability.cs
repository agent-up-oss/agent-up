namespace AgentUp.Desktop.Features.Authentication.DTOs;

public sealed record ClientSurfaceAvailability(
    bool Database,
    bool Diagnostics,
    bool Validation,
    bool Metrics)
{
    public static ClientSurfaceAvailability Real { get; } = new(true, true, true, true);

    public static ClientSurfaceAvailability Demo { get; } = new(false, false, false, false);

    public static ClientSurfaceAvailability ForActiveServer(bool isDemo)
        => isDemo ? Demo : Real;
}
