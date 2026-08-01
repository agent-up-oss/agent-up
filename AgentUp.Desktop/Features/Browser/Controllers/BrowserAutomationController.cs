using AgentUp.Desktop.Features.Browser.Services;

namespace AgentUp.Desktop.Features.Browser.Controllers;

internal sealed class BrowserAutomationController(BrowserCommandPoller poller)
{
    public void Start() => poller.Start();

    public void Stop() => poller.Stop();
}
