using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Features.Screens.Models;

namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>
/// The page-assembly screens and the route that leaves each one in a used state.
/// </summary>
/// <remarks>
/// The screen set is the one the design system documents under Assembled screens; the ids
/// match its scenes, and <c>ProductScreenCatalogTests</c> fails if the two drift apart.
/// <para>
/// Routes are ordered and cumulative. Prompting the agent makes the Demo backend add a file
/// to the working tree, so Git is captured after Agent and already shows that file; the
/// commit the Git route composes is what History is captured after. Changing the order
/// changes what the later screens show.
/// </para>
/// <para>
/// Four Desktop screens are marked unavailable. Demo has no Database, Diagnostics, or
/// Metrics tab, and Validation is a collapsed sidebar on the selected application rather
/// than a dedicated screen. They stay listed, with their reason, rather than being
/// dropped, so a run still accounts for every documented screen.
/// </para>
/// </remarks>
public sealed class ProductScreenCatalog : IProductScreenCatalog
{
    private const string DemoHidesTab =
        "The Demo connection reports no Database, Diagnostics, or Metrics entitlement, "
        + "so Desktop does not build this tab. Capture it against a real Server workspace.";

    private const string ValidationIsSidebar =
        "Desktop validation is a collapsed sidebar on the selected application, not a dedicated tab, "
        + "so there is no Validation screen to photograph on Demo.";

    private const string FirstPrompt = "What is running in this workspace?";
    private const string SecondPrompt = "Add a promo banner to the storefront.";
    private const string CommitMessage = "feat(storefront): add the weekly promo banner";
    private const string ReviewMessage = "fix(storefront): correct the featured product grid";
    private const string PromoBannerPath = "apps/storefront/PromoBanner.tsx";
    private const string SecondPromoBannerPath = "apps/storefront/PromoBanner2.tsx";
    private const string ProductGridPath = "apps/storefront/ProductGrid.tsx";
    private const string OrdersPath = "apps/api/orders.ts";

    public IReadOnlyList<string> Surfaces => [ProductSurface.Desktop, ProductSurface.Mobile];

    public IReadOnlyList<ProductScreenDto> Screens(string surface)
        => surface == ProductSurface.Desktop ? Desktop() : Mobile();

    private static IReadOnlyList<ProductScreenDto> Desktop() =>
    [
        DesktopScreen("sign-in", "Desktop sign-in",
        [
            ScreenStepDto.Click(DesktopScreenGeometry.SignInUrlFieldX, DesktopScreenGeometry.SignInUrlFieldY),
            ScreenStepDto.Key("ctrl+a"),
            ScreenStepDto.Type(DebugLayout.DemoServerUrl),
            ScreenStepDto.Settle(800)
        ]),
        DesktopScreen("workspaces", "Desktop workspaces",
        [
            ScreenStepDto.Click(DesktopScreenGeometry.SignInDemoRowX, DesktopScreenGeometry.SignInDemoRowY, 3000)
        ]),
        DesktopScreen("agents", "Desktop Agent",
        [
            ScreenStepDto.Click(DesktopScreenGeometry.AgentTabX, DesktopScreenGeometry.TabRowY, 2000),
            .. Prompt(FirstPrompt),
            .. Prompt(SecondPrompt)
        ]),
        DesktopScreen("applications", "Desktop applications",
        [
            ScreenStepDto.Click(DesktopScreenGeometry.StorefrontTabX, DesktopScreenGeometry.TabRowY, 4000),
            ScreenStepDto.Click(DesktopScreenGeometry.StorefrontFirstAddToCartX, DesktopScreenGeometry.StorefrontFirstAddToCartY, 1500),
            ScreenStepDto.Click(DesktopScreenGeometry.StorefrontSecondAddToCartX, DesktopScreenGeometry.StorefrontSecondAddToCartY, 1500)
        ]),
        DesktopScreen("console", "Desktop console",
        [
            ScreenStepDto.Click(DesktopScreenGeometry.ConsoleSubTabX, DesktopScreenGeometry.SubTabRowY, 1500)
        ]),
        DesktopScreen("git", "Desktop Git changes",
        [
            ScreenStepDto.Click(DesktopScreenGeometry.GitTabX, DesktopScreenGeometry.TabRowY, 3000),
            ScreenStepDto.Click(DesktopScreenGeometry.GitTreeCheckboxX, AgentFileRow(0)),
            ScreenStepDto.Click(DesktopScreenGeometry.GitTreeCheckboxX, AgentFileRow(1)),
            ScreenStepDto.Click(DesktopScreenGeometry.GitCommitMessageX, DesktopScreenGeometry.GitCommitMessageY),
            ScreenStepDto.Type(CommitMessage),
            ScreenStepDto.Settle(800)
        ]),
        DesktopScreen("file-viewer", "Desktop file viewer",
        [
            ScreenStepDto.Click(DesktopScreenGeometry.GitTreeNameX, AgentFileRow(0), 2500),
            ScreenStepDto.Click(DesktopScreenGeometry.FileViewerLineFieldX, DesktopScreenGeometry.FileViewerLineFieldY),
            ScreenStepDto.Type("3"),
            ScreenStepDto.Click(DesktopScreenGeometry.FileViewerGoX, DesktopScreenGeometry.FileViewerLineFieldY, 1500)
        ]),
        DesktopScreen("history", "Desktop Git history",
        [
            ScreenStepDto.Click(DesktopScreenGeometry.FileViewerCloseX, DesktopScreenGeometry.FileViewerCloseY, 1500),
            ScreenStepDto.Click(DesktopScreenGeometry.GitCommitMessageX, DesktopScreenGeometry.GitCommitButtonY, 3000),
            ScreenStepDto.Click(DesktopScreenGeometry.GitHistoryButtonX, DesktopScreenGeometry.GitHistoryButtonAfterCommitY, 2500)
        ]),
        DesktopScreen("capabilities", "Desktop capabilities",
        [
            ScreenStepDto.Click(DesktopScreenGeometry.GitHistoryBackX, DesktopScreenGeometry.GitHistoryBackY, 1500),
            ScreenStepDto.Click(DesktopScreenGeometry.CapabilityModulesButtonX, DesktopScreenGeometry.ChromeButtonY, 2000),
            ScreenStepDto.Click(DesktopScreenGeometry.CapabilityActionX, DesktopScreenGeometry.CapabilityCursorRowY, 2000)
        ]),
        Unavailable("diagnostics", "Desktop diagnostics"),
        Unavailable("metrics", "Desktop metrics"),
        Unavailable("validation", "Desktop validation", ValidationIsSidebar),
        Unavailable("database", "Desktop database")
    ];

    private static IReadOnlyList<ProductScreenDto> Mobile() =>
    [
        MobileScreen("sign-in", "Mobile sign-in",
        [
            ScreenStepDto.Fill("Server URL", DebugLayout.DemoServerUrl),
            ScreenStepDto.Settle(800)
        ]),
        MobileScreen("apps", "Mobile apps",
        [
            ScreenStepDto.Tap(DebugLayout.DemoServerName, 3500)
        ]),
        MobileScreen("workspaces", "Mobile workspaces",
        [
            ScreenStepDto.Tap("Open sidebar", 1500)
        ]),
        MobileScreen("agents", "Mobile Agents",
        [
            ScreenStepDto.Tap("Close sidebar", 1200),
            ScreenStepDto.Tap("Agents", 1500),
            ScreenStepDto.Tap("Codex", 2500),
            .. MobilePrompt(FirstPrompt),
            .. MobilePrompt(SecondPrompt),
            ScreenStepDto.Tap("Go back", 2500)
        ]),
        MobileScreen("git", "Mobile Git",
        [
            ScreenStepDto.Tap("Git", 2500),
            .. SelectAgentFiles(),
            ScreenStepDto.Fill("Commit message", CommitMessage),
            ScreenStepDto.Settle(800)
        ]),
        MobileScreen("file-viewer", "Mobile file viewer",
        [
            ScreenStepDto.Tap($"Open {PromoBannerPath}", 2500)
        ]),
        // History comes before Review because opening Review assigns the agent's files into
        // the proposal queue, which takes them out of the working tree the commit needs.
        MobileScreen("history", "Mobile Git history",
        [
            ScreenStepDto.Tap("Close file viewer", 1500),
            // The panel repolls every 2.5s and clears the confirmation when it does, so the
            // confirm step follows immediately rather than after a settle.
            ScreenStepDto.Tap("Commit +2", 200),
            ScreenStepDto.Tap("Commit", 3000),
            ScreenStepDto.Tap("History", 2500)
        ]),
        MobileScreen("review", "Mobile Git review",
        [
            ScreenStepDto.Tap("Go back", 2500),
            ScreenStepDto.Navigate($"/workspace/{DebugLayout.DemoWorkspaceId}/git/review"),
            ScreenStepDto.Tap($"Select {ProductGridPath}"),
            ScreenStepDto.Tap($"Select {OrdersPath}"),
            ScreenStepDto.Fill("Commit message", ReviewMessage),
            ScreenStepDto.Settle(800)
        ]),
        MobileScreen("settings", "Mobile settings",
        [
            ScreenStepDto.Tap("Go back", 2000),
            ScreenStepDto.Navigate($"/workspace/{DebugLayout.DemoWorkspaceId}"),
            ScreenStepDto.Tap("Settings", 2000),
            ScreenStepDto.Tap("Disable", 1500)
        ])
    ];

    private static ScreenStepDto[] MobilePrompt(string text) =>
    [
        ScreenStepDto.Fill("Ask the agent", text),
        ScreenStepDto.Tap("Send", 2500)
    ];

    /// <summary>Ticks the two files the Agent route asked for, by the label the row carries.</summary>
    private static ScreenStepDto[] SelectAgentFiles() =>
    [
        ScreenStepDto.Tap($"Select {PromoBannerPath}"),
        ScreenStepDto.Tap($"Select {SecondPromoBannerPath}")
    ];

    private static ScreenStepDto[] Prompt(string text) =>
    [
        ScreenStepDto.Click(DesktopScreenGeometry.AgentPromptX, DesktopScreenGeometry.AgentPromptY),
        ScreenStepDto.Type(text),
        ScreenStepDto.Click(DesktopScreenGeometry.AgentSendX, DesktopScreenGeometry.AgentPromptY, 3000)
    ];

    private static int AgentFileRow(int offset)
        => DesktopScreenGeometry.GitTreeRowY(DesktopScreenGeometry.GitTreeFirstAgentFileRow + offset);

    private static ProductScreenDto DesktopScreen(string view, string title, IReadOnlyList<ScreenStepDto> steps)
        => new()
        {
            Id = $"{ProductSurface.Desktop}-{view}",
            Surface = ProductSurface.Desktop,
            View = view,
            Title = title,
            Width = DebugLayout.DesktopScreenshotWidth,
            Height = DebugLayout.DesktopScreenshotHeight,
            Steps = steps
        };

    private static ProductScreenDto MobileScreen(string view, string title, IReadOnlyList<ScreenStepDto> steps)
        => new()
        {
            Id = $"{ProductSurface.Mobile}-{view}",
            Surface = ProductSurface.Mobile,
            View = view,
            Title = title,
            Width = DebugLayout.MobileScreenWidth,
            Height = DebugLayout.MobileScreenHeight,
            Steps = steps
        };

    private static ProductScreenDto Unavailable(string view, string title, string? reason = null)
        => DesktopScreen(view, title, []) with { Available = false, UnavailableReason = reason ?? DemoHidesTab };
}
