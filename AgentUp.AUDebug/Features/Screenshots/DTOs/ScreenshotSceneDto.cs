namespace AgentUp.AUDebug.Features.Screenshots.DTOs;

public sealed record ScreenshotSceneDto
{
    public string Id { get; init; } = "";
    public string Surface { get; init; } = "";
    public string View { get; init; } = "";
    public string Title { get; init; } = "";
    public string MediaFile { get; init; } = "";
    public string HtmlFile { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public bool Hero { get; init; }
    public string LivePath { get; init; } = "";
    public IReadOnlyList<string> Components { get; init; } = [];
    public IReadOnlyList<string> RequiredClasses { get; init; } = [];

    /// <summary>
    /// The modifier classes the scene's components declare as states, which the scene is
    /// therefore allowed to have moved relative to the catalog example.
    /// </summary>
    public IReadOnlyList<string> StateModifiers { get; init; } = [];

    /// <summary>The text this scene puts on screen, which <c>--live</c> looks for on the real page.</summary>
    public IReadOnlyList<string> Copy { get; init; } = [];

    /// <summary>
    /// The resolved markup of each component whose copy this scene replaced, keyed by component
    /// id. A state only moves a class and can be neutralised, but a replaced title cannot be
    /// re-derived from the catalog, so the generator records what it produced and the contract
    /// compares against that rather than dropping to a weaker check.
    /// </summary>
    public IReadOnlyDictionary<string, string> ComponentFragments { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}
