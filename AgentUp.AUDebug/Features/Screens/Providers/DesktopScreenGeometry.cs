namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>
/// Window-relative click points for the Desktop page-assembly route.
/// </summary>
/// <remarks>
/// Desktop is an X11 window, so a route points at it. Every value here was read off a real
/// 1440x900 Demo session rather than derived from the AXAML, which is why they live in one
/// place: a layout change moves these numbers and nothing else. The route always sizes the
/// window to <c>DesktopScreenshotWidth</c> x <c>DesktopScreenshotHeight</c> first, so the
/// points hold whatever size the window manager handed the app.
/// </remarks>
public static class DesktopScreenGeometry
{
    /// <summary>Sign-in pane, centred in the content area under the 42px chrome.</summary>
    public const int SignInUrlFieldX = 720;

    public const int SignInUrlFieldY = 485;
    public const int SignInDemoRowX = 716;
    public const int SignInDemoRowY = 555;

    /// <summary>Chrome bar: 46x32 buttons from a 10px left margin, vertically centred in 42px.</summary>
    public const int ChromeButtonY = 21;

    public const int CapabilityModulesButtonX = 61;

    /// <summary>Shell and application tab strip. One row, 52px tall, starting under the chrome.</summary>
    public const int TabRowY = 65;

    public const int OverviewTabX = 275;
    public const int AgentTabX = 373;
    public const int GitTabX = 450;
    public const int StorefrontTabX = 575;

    /// <summary>Application sub-navbar, below the tab strip.</summary>
    public const int SubTabRowY = 117;

    public const int ConsoleSubTabX = 377;

    /// <summary>Agent composer, pinned to the bottom of the Agent tab.</summary>
    public const int AgentPromptX = 780;

    public const int AgentPromptY = 857;
    public const int AgentSendX = 1374;

    /// <summary>Demo storefront page, inside the port WebView.</summary>
    public const int StorefrontFirstAddToCartX = 540;

    public const int StorefrontFirstAddToCartY = 513;
    public const int StorefrontSecondAddToCartX = 785;
    public const int StorefrontSecondAddToCartY = 532;

    /// <summary>Git change tree: evenly pitched rows under a fixed header.</summary>
    public const int GitTreeFirstRowY = 247;

    public const int GitTreeRowPitch = 36;
    public const int GitTreeCheckboxX = 247;
    public const int GitTreeNameX = 378;

    /// <summary>Row index of the first agent-added file once the Agent route has run twice.</summary>
    public const int GitTreeFirstAgentFileRow = 4;

    public const int GitCommitMessageX = 830;
    public const int GitCommitMessageY = 798;
    public const int GitCommitButtonY = 863;
    public const int GitHistoryButtonX = 1390;
    public const int GitHistoryButtonY = 158;

    /// <summary>
    /// After a commit the panel grows an ahead/behind summary row, which pushes the action row
    /// down by one line. History is only ever clicked post-commit, so the route uses this value.
    /// </summary>
    public const int GitHistoryButtonAfterCommitY = 179;

    public const int GitHistoryBackX = 265;
    public const int GitHistoryBackY = 119;

    /// <summary>File diff overlay.</summary>
    public const int FileViewerLineFieldX = 298;

    public const int FileViewerLineFieldY = 195;
    public const int FileViewerGoX = 367;
    public const int FileViewerCloseX = 1256;
    public const int FileViewerCloseY = 789;

    /// <summary>Capability modules overlay: five rows, action buttons right-aligned.</summary>
    public const int CapabilityActionX = 1219;

    public const int CapabilityCursorRowY = 537;

    public static int GitTreeRowY(int row) => GitTreeFirstRowY + (row * GitTreeRowPitch);
}
