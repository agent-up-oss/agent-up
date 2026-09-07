using AgentUp.Desktop.Features.Workspaces.ViewModels.Chrome;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WindowChromeViewModelTests
{
    [Test]
    public void SetLeftItems_ReplacesRegisteredItems()
    {
        var chrome = new WindowChromeViewModel();
        chrome.SetLeftItems([new object(), new object()]);

        chrome.SetLeftItems([new object()]);

        Assert.That(chrome.LeftItems, Has.Count.EqualTo(1));
    }
}
