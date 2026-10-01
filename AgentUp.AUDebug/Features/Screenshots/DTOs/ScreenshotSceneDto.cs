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
    public IReadOnlyList<string> AppSources { get; init; } = [];
    public IReadOnlyList<string> RequiredClasses { get; init; } = [];
    public IReadOnlyList<string> RequiredDesktopClasses { get; init; } = [];
    public IReadOnlyList<string> RequiredMobileComponents { get; init; } = [];
    public IReadOnlyList<string> Copy { get; init; } = [];
}
