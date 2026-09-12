using System.Text.Json;
using AgentUp.Desktop.Features.Agents.Providers;

namespace AgentUp.Desktop.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentEventPresentationProviderTests
{
    [Test]
    public void Present_keepsSessionChromeOutOfTheTranscript()
    {
        Assert.That(Present("""{"sessionUpdate":"session_info_update","title":"Test conversation title"}""").Kind, Is.EqualTo("context"));
        Assert.That(Present("""{"sessionUpdate":"session_info_update","title":"Test conversation title"}""").Title, Is.EqualTo("Test conversation title"));
        Assert.That(Present("""{"sessionUpdate":"current_mode_update","currentModeId":"ask"}""").Mode, Is.EqualTo("ask"));
        Assert.That(Present("""{"sessionUpdate":"usage_update","used":12400,"size":200000}""").Usage, Is.EqualTo("12k / 200k"));
        Assert.That(Present("""{"sessionUpdate":"available_commands_update"}""").Kind, Is.EqualTo("ignore"));
    }

    [Test]
    public void Present_classifiesThoughtsToolsAndMessages()
    {
        Assert.That(Present("""{"sessionUpdate":"agent_thought_chunk","content":{"text":"Planning"}}""").Role, Is.EqualTo("Thought"));
        Assert.That(Present("""{"sessionUpdate":"tool_call_update","toolCallId":"call-1","title":"Read file","status":"completed"}""").Kind, Is.EqualTo("tool"));
        Assert.That(Present("""{"update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"hello"}}}""", unwrap: true).Text, Is.EqualTo("hello"));
    }

    [Test]
    public void ParsePermission_acceptsLegacyToolCallAndCurrentPromptShapes()
    {
        var legacy = Parse("""{"requestId":"req-1","request":{"toolCall":{"title":"Edit file","kind":"edit","locations":[{"path":"/repo/a.ts"}]},"options":[{"optionId":"allow","name":"Allow once","kind":"allow_once"}]}}""");
        var current = Parse("""{"requestId":"req-2","request":{"title":"Run the test suite?","description":"cargo test","subject":{"type":"command","command":"cargo test","cwd":"/repo"},"options":[{"optionId":"allow-once","kind":"allow_once"}]}}""");

        Assert.Multiple(() =>
        {
            Assert.That(legacy!.Title, Is.EqualTo("Edit file"));
            Assert.That(legacy.Locations, Does.Contain("/repo/a.ts"));
            Assert.That(current!.Title, Is.EqualTo("Run the test suite?"));
            Assert.That(AgentEventPresentationProvider.OptionLabel(current.Options[0].Name, current.Options[0].Kind, current.Options[0].OptionId), Is.EqualTo("Allow once"));
        });
    }

    [Test]
    public void ActivityLabel_usesPermissionAndLatestUpdateWhileRunning()
    {
        Assert.That(AgentEventPresentationProvider.ActivityLabel("ready", false, null, null, null), Is.EqualTo("Idle"));
        Assert.That(AgentEventPresentationProvider.ActivityLabel("running", true, null, "Thought", null), Is.EqualTo("Waiting for a decision"));
        Assert.That(AgentEventPresentationProvider.ActivityLabel("running", false, null, "Thought", null), Is.EqualTo("Thinking"));
        Assert.That(AgentEventPresentationProvider.ActivityLabel("running", false, null, "Tool", "Read file"), Is.EqualTo("Using Read file"));
    }

    private static PresentedAgentUpdate Present(string json, bool unwrap = false)
    {
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement.Clone();
        return AgentEventPresentationProvider.Present(unwrap ? AgentEventPresentationProvider.Unwrap(payload) : payload);
    }

    private static AgentPermissionPrompt? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return AgentEventPresentationProvider.ParsePermission(document.RootElement.Clone());
    }
}
