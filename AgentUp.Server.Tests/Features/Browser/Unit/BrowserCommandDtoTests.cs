using AgentUp.Server.Features.Browser.Models;

namespace AgentUp.Server.Tests.Features.Browser.Unit;

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
            BrowserCommandKind.Fill,
            "https://example.test",
            "#name",
            "Ada",
            "Enter",
            123);

        Assert.Multiple(() =>
        {
            Assert.That(command.CommandId, Is.EqualTo(id));
            Assert.That(command.WorkspaceId, Is.EqualTo("workspace"));
            Assert.That(command.Kind, Is.EqualTo(BrowserCommandKind.Fill));
            Assert.That(command.Url, Is.EqualTo("https://example.test"));
            Assert.That(command.Selector, Is.EqualTo("#name"));
            Assert.That(command.Text, Is.EqualTo("Ada"));
            Assert.That(command.Key, Is.EqualTo("Enter"));
            Assert.That(command.TimeoutMs, Is.EqualTo(123));
            Assert.That(command.ReloadIfSameUrl, Is.True);
        });
    }
}
