using PuppeteerSharp;

namespace AgentUp.Server.Features.Browser.Models;

public sealed record BrowserSessionState(string WorkspaceId, IBrowser Browser, IPage Page)
{
    public string CurrentUrl => Page.Url;

    public Task GoBackAsync(CancellationToken ct) => Page.GoBackAsync().WaitAsync(ct);
    public Task GoForwardAsync(CancellationToken ct) => Page.GoForwardAsync().WaitAsync(ct);
    public Task ReloadAsync(CancellationToken ct) => Page.ReloadAsync().WaitAsync(ct);
}
