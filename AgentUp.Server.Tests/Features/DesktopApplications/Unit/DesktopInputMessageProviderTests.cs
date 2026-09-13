using AgentUp.Server.Features.DesktopApplications.Providers;

namespace AgentUp.Server.Tests.Features.DesktopApplications.Unit;

[TestFixture]
public sealed class DesktopInputMessageProviderTests
{
    [Test]
    public void Parses_supported_fields_and_rejects_invalid_json()
    {
        var provider = new DesktopInputMessageProvider();

        var message = provider.Parse("""{"type":"pointerDown","x":12,"y":34,"button":1,"key":"A"}""");

        Assert.Multiple(() =>
        {
            Assert.That(message, Is.Not.Null);
            Assert.That(message!.Type, Is.EqualTo("pointerDown"));
            Assert.That(message.X, Is.EqualTo(12));
            Assert.That(message.Y, Is.EqualTo(34));
            Assert.That(message.Button, Is.EqualTo(1));
            Assert.That(message.Key, Is.EqualTo("A"));
            Assert.That(provider.Parse("{"), Is.Null);
            Assert.That(provider.Parse("""{"type":"wheel","deltaX":1.5,"deltaY":-2}""")!.DeltaX, Is.EqualTo(1.5));
            Assert.That(provider.Parse("""{"type":"wheel","deltaX":1.5,"deltaY":-2}""")!.DeltaY, Is.EqualTo(-2));
        });
    }
}
