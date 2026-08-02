using PuppeteerSharp;

namespace AgentUp.Server.Features.Browser.Models;

public sealed record BrowserSessionState(string WorkspaceId, IBrowser Browser, IPage Page)
{
    public string CurrentUrl => Page.Url;

    private static readonly NavigationOptions NavOptions = new() { Timeout = 30000 };

    public Task GoBackAsync(CancellationToken ct) => Page.GoBackAsync(NavOptions).WaitAsync(ct);
    public Task GoForwardAsync(CancellationToken ct) => Page.GoForwardAsync(NavOptions).WaitAsync(ct);
    public Task ReloadAsync(CancellationToken ct) => Page.ReloadAsync(timeout: 30000).WaitAsync(ct);
}
