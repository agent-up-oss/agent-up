using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.Models;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Unit;

[TestFixture]
public sealed class FakeServerDefinitionTests
{
    [Test]
    public void Constructor_requiresCatalogFields()
    {
        var root = JsonNode.Parse("""{"id":"fake"}""")!;

        Assert.That(
            () => new FakeServerDefinition(root).DisplayName,
            Throws.InvalidOperationException.With.Message.EqualTo("The fake server definition is missing 'displayName'."));
        Assert.That(
            () => new FakeServerDefinition(root).Connection,
            Throws.InvalidOperationException.With.Message.EqualTo("The fake server definition is missing 'connection'."));
    }

    [Test]
    public void Clone_copiesWorkspacesIndependently()
    {
        var root = JsonNode.Parse(
            """{"id":"fake","url":"http://127.0.0.1:9","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":[]}""")!;
        var definition = new FakeServerDefinition(root);
        var clone = definition.Clone();
        definition.Workspaces.Add("changed");

        Assert.That(clone.Workspaces, Is.Empty);
        Assert.That(definition.Workspaces, Has.Count.EqualTo(1));
        Assert.That(definition.Id, Is.EqualTo("fake"));
    }
}

[TestFixture]
public sealed class FakeServerEventTests
{
    [Test]
    public void ToSseFrame_includesSequenceTypeAndPayload()
    {
        var frame = new FakeServerEvent(3, "user_message", JsonNode.Parse("""{"text":"hi"}""")!, DateTimeOffset.UnixEpoch)
            .ToSseFrame();

        Assert.That(frame, Does.StartWith("data: "));
        Assert.That(frame, Does.Contain("user_message"));
        Assert.That(frame, Does.Contain("\"sequence\":3"));
        Assert.That(frame, Does.EndWith("\n\n"));
    }
}
