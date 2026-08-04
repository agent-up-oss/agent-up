using PuppeteerSharp;

namespace AgentUp.Server.Features.Browser.Models;

public sealed class BrowserSessionState
{
    private readonly Dictionary<string, IPage> _pagesByOrigin = new(StringComparer.Ordinal);

    public BrowserSessionState(string workspaceId, IBrowser browser, IPage page)
    {
        WorkspaceId = workspaceId;
        Browser = browser;
        Page = page;
    }

    public string WorkspaceId { get; }
    public IBrowser Browser { get; }
    public IPage Page { get; private set; }

    public string CurrentUrl => Page.Url;

    private static readonly NavigationOptions NavOptions = new() { Timeout = 30000 };

    public async Task<IPage> ActivatePageForUrlAsync(Uri uri, CancellationToken ct)
    {
        var pageKey = PageKeyForUrl(uri);
        if (_pagesByOrigin.TryGetValue(pageKey, out var cached) && !cached.IsClosed)
        {
            Page = cached;
            return cached;
        }

        var page = Page.Url == "about:blank" && !_pagesByOrigin.ContainsValue(Page)
            ? Page
            : await Browser.NewPageAsync().WaitAsync(ct);
        _pagesByOrigin[pageKey] = page;
        Page = page;
        return page;
    }

    public async Task SetViewportAsync(ViewPortOptions viewport, CancellationToken ct)
    {
        var pages = _pagesByOrigin.Values
            .Append(Page)
            .Distinct()
            .Where(page => !page.IsClosed)
            .ToArray();

        foreach (var page in pages)
            await page.SetViewportAsync(viewport).WaitAsync(ct);
    }

    public Task GoBackAsync(CancellationToken ct) => Page.GoBackAsync(NavOptions).WaitAsync(ct);
    public Task GoForwardAsync(CancellationToken ct) => Page.GoForwardAsync(NavOptions).WaitAsync(ct);
    public Task ReloadAsync(CancellationToken ct) => Page.ReloadAsync(timeout: 30000).WaitAsync(ct);

    internal static string PageKeyForUrl(Uri uri)
        => uri.GetLeftPart(UriPartial.Authority);
}
