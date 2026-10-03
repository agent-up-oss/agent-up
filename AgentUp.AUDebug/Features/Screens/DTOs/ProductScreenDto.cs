namespace AgentUp.AUDebug.Features.Screens.DTOs;

/// <summary>
/// One screen of the design-system page assembly, plus the route that puts the real client
/// into the used state that screen is about.
/// </summary>
/// <remarks>
/// <see cref="Available"/> is false for a screen the Demo backend does not expose. Those are
/// reported with <see cref="UnavailableReason"/> rather than silently dropped, so the run
/// still accounts for every page-assembly screen.
/// </remarks>
public sealed record ProductScreenDto
{
    public string Id { get; init; } = "";
    public string Surface { get; init; } = "";
    public string View { get; init; } = "";
    public string Title { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public bool Available { get; init; } = true;
    public string UnavailableReason { get; init; } = "";
    public IReadOnlyList<ScreenStepDto> Steps { get; init; } = [];
}
