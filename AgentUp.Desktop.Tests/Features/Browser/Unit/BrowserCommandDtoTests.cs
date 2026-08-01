using AgentUp.Desktop.Features.Browser.Models;

namespace AgentUp.Desktop.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class BrowserCommandDtoTests
{
    [Test]
    public void Constructor_PreservesCommandValues()
    {
        var id = Guid.NewGuid();
        var command = new BrowserCommandDto(
            id,
            "workspace",
            BrowserCommandKind.Click,
            "https://example.test",
            "#save",
            "text",
            "Enter",
            123);

        Assert.That(command.CommandId, Is.EqualTo(id));
        Assert.That(command.WorkspaceId, Is.EqualTo("workspace"));
        Assert.That(command.Kind, Is.EqualTo(BrowserCommandKind.Click));
        Assert.That(command.Url, Is.EqualTo("https://example.test"));
        Assert.That(command.Selector, Is.EqualTo("#save"));
        Assert.That(command.Text, Is.EqualTo("text"));
        Assert.That(command.Key, Is.EqualTo("Enter"));
        Assert.That(command.TimeoutMs, Is.EqualTo(123));
    }
}
