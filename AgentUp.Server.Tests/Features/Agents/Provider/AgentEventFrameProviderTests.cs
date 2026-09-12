using System.Text.Json;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentEventFrameProviderTests
{
    [Test]
    public void Frame_usesCamelCaseSoWebClientsCanReadTypeAndPayload()
    {
        var frames = new AgentEventFrameProvider();
        var item = new AgentUp.Server.Features.Agents.DTOs.AgentEventDto(
            3, "user_message", frames.Payload(new { text = "hello" }), DateTimeOffset.UnixEpoch);

        var frame = frames.Frame(item);

        Assert.That(frame, Does.Contain("event: user_message"));
        using var document = JsonDocument.Parse(frame.Split('\n').Single(line => line.StartsWith("data: ", StringComparison.Ordinal))[6..]);
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("sequence").GetInt64(), Is.EqualTo(3));
            Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo("user_message"));
            Assert.That(document.RootElement.GetProperty("payload").GetProperty("text").GetString(), Is.EqualTo("hello"));
        });
    }

    [Test]
    public void Payload_usesCamelCaseForSessionSnapshots()
    {
        var payload = new AgentEventFrameProvider().Payload(
            new AgentUp.Server.Features.Agents.DTOs.AgentSessionDto(
                "ws", AgentUp.Server.Features.Agents.DTOs.AgentKind.Codex, "running", "session-1", null, [], []));

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("sessionId").GetString(), Is.EqualTo("session-1"));
            Assert.That(payload.GetProperty("state").GetString(), Is.EqualTo("running"));
            Assert.That(payload.GetProperty("agent").GetString(), Is.EqualTo("Codex"));
        });
    }
}
