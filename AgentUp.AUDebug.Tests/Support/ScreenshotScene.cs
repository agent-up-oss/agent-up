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
            AppSources = ["AgentUp.Desktop/Features/Workspaces/Views/MainWindow.axaml"],
            RequiredClasses = ["au-workspace"],
            RequiredDesktopClasses = ["wsEntry"],
            RequiredMobileComponents = [],
            Copy = ["Harbor Shop"]
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
            AppSources = ["AgentUp.Mobile/src/features/shell/components/WorkspaceTabBar.tsx"],
            RequiredClasses = ["au-mobile-tab-bar"],
            RequiredDesktopClasses = [],
            RequiredMobileComponents = ["mobileTabBar"],
            Copy = ["Harbor Shop"]
        };
}
