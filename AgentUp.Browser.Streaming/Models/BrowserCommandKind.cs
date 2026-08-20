namespace AgentUp.Browser.Streaming.Models;

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
