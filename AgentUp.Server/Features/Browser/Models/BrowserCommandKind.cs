namespace AgentUp.Server.Features.Browser.Models;

public enum BrowserCommandKind
{
    Navigate,
    InspectPage,
    Click,
    Fill,
    Press,
    WaitForSelector,
    WaitForText,
    WaitForNavigation,
    Screenshot
}
