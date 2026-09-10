using AgentUp.Desktop.Features.Browser.Resources;

namespace AgentUp.Desktop.Features.Browser.Controllers;

public sealed class BrowserInteractionController
{
    public int AnimationMs => BrowserScripts.AnimationMs;

    public string BeginMouseMoveScript(string selector) => BrowserScripts.BeginMouseMove(selector);

    public string BeginAttentionPingScript(string selector) => BrowserScripts.BeginAttentionPing(selector);

    public string CompleteClickScript(string selector) => BrowserScripts.CompleteClick(selector);

    public string FillScript(string selector, string text) => BrowserScripts.Fill(selector, text);

    public string PressScript(string key) => BrowserScripts.Press(key);

    public string RemoveAttentionPingScript() => BrowserScripts.RemoveAttentionPing();

    public string CheckNavigationScript() => BrowserScripts.CheckNavigation;

    public string CheckTextScript(string text) => BrowserScripts.CheckText(text);

    public string CheckSelectorScript(string selector) => BrowserScripts.CheckSelector(selector);

    public string GetUrlScript() => BrowserScripts.GetUrl;

    public string GetTitleScript() => BrowserScripts.GetTitle;
}
