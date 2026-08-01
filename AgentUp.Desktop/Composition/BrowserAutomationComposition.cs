using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Browser.Providers;
using AgentUp.Desktop.Features.Browser.Services;
using AgentUp.Desktop.Shared.Interfaces;

namespace AgentUp.Desktop.Composition;

internal static class BrowserAutomationComposition
{
    public static BrowserAutomationController Create(HttpClient http, IBrowserWindowHost host) =>
        new(new BrowserCommandPoller(new BrowserCommandHttpClient(http), host));
}
