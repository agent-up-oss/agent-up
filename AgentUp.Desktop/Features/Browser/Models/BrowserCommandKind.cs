namespace AgentUp.Desktop.Features.Browser.Models;

internal enum BrowserCommandKind
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
