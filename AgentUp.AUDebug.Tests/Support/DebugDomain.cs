namespace AgentUp.AUDebug.Tests.Support;

/// <summary>
/// The shared domain vocabulary for AUDebug tests: the surfaces the visual host drives and
/// the workspace it drives them against, named once and reused everywhere.
/// </summary>
/// <remarks>
/// Tests refer to these names rather than repeating verb and surface strings, so a reader
/// can tell at a glance whether two tests are driving the same surface.
/// </remarks>
internal static class DebugDomain
{
    public const string DocsSurface = "docs";
    public const string MobileSurface = "mobile";
    public const string DesktopSurface = "desktop";

    public const string ScreensVerb = "screens";
    public const string ScreenshotAction = "screenshot";
    public const string OpenAgentAction = "open-agent";
    public const string LoginAction = "login";
    public const string StartWorkspaceAction = "start-workspace";

    public const string WorkspaceName = "Agent-Up";
    public const string Password = "test";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>A command against a surface: the verb and the surface are the same word.</summary>
    public static DebugCommandDtoBuilder Command(string surface) => new(surface, surface);

    /// <summary>A screenshots persist/validate/capture command.</summary>
    public static DebugCommandDtoBuilder Screenshots(string action) => new DebugCommandDtoBuilder("screenshots", action).Doing(action);

    /// <summary>A product-screens command for one surface, or for every surface.</summary>
    public static DebugCommandDtoBuilder Screens(string surface = "all")
        => new DebugCommandDtoBuilder(ScreensVerb, surface == "all" ? null : surface).Doing(surface);

    /// <summary>
    /// A screens comparison, optionally scoped to one surface. Compare carries the scope on the
    /// surface and always names "compare" as its action, which is the shape the parser produces.
    /// </summary>
    public static DebugCommandDtoBuilder ScreensCompare(string surface = "all")
        => new DebugCommandDtoBuilder(ScreensVerb, surface == "all" ? null : surface).Doing("compare");

    /// <summary>A command with no surface, such as <c>test</c> or <c>build</c>.</summary>
    public static DebugCommandDtoBuilder Verb(string verb) => new(verb, null);
}
