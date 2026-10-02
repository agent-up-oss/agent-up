using AgentUp.AUDebug.Features.Screenshots.DTOs;

namespace AgentUp.AUDebug.Tests.Support;

internal static class ScreenshotScene
{
    public static ScreenshotSceneDto Desktop(string view = "applications", bool hero = true)
        => new()
        {
            Id = $"desktop-{view}",
            Surface = "desktop",
            View = view,
            Title = $"Desktop {view}",
            MediaFile = $"desktop-{view}.png",
            HtmlFile = $"desktop-{view}.html",
            Width = 1440,
            Height = 900,
            Hero = hero,
            LivePath = "",
            Components = ["workspace"],
            RequiredClasses = ["au-workspace"]
        };

    public static ScreenshotSceneDto Mobile(string view = "apps")
        => new()
        {
            Id = $"mobile-{view}",
            Surface = "mobile",
            View = view,
            Title = $"Mobile {view}",
            MediaFile = $"mobile-{view}.png",
            HtmlFile = $"mobile-{view}.html",
            Width = 390,
            Height = 844,
            Hero = false,
            LivePath = $"/workspace/harbor-shop/{view}",
            Components = ["mobile-tab-bar"],
            RequiredClasses = ["au-mobile-tab-bar"],
        };
}
