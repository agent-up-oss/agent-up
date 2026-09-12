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
        Assert.That(AgentEventPresentationProvider.ActivityLabel("running", false, null, "Tool", null), Is.EqualTo("Using a tool"));
        Assert.That(AgentEventPresentationProvider.ActivityLabel("running", false, null, "Agent", null), Is.EqualTo("Writing"));
        Assert.That(AgentEventPresentationProvider.ActivityLabel("running", false, null, "Plan", null), Is.EqualTo("Working through the plan"));
        Assert.That(AgentEventPresentationProvider.ActivityLabel("running", false, null, "Compacting", null), Is.EqualTo("Compacting context"));
        Assert.That(AgentEventPresentationProvider.ActivityLabel("running", false, null, null, null), Is.EqualTo("Working"));
        Assert.That(AgentEventPresentationProvider.ActivityLabel("stopped", false, null, null, null), Is.EqualTo("Stopped"));
        Assert.That(AgentEventPresentationProvider.ActivityLabel("authentication_required", false, null, null, null), Is.EqualTo("Waiting for sign-in"));
        Assert.That(AgentEventPresentationProvider.ActivityLabel("ready", false, "boom", null, null), Is.EqualTo("boom"));
    }

    [Test]
    public void OptionLabel_fallsBackToKindThenOptionId()
    {
        Assert.That(AgentEventPresentationProvider.OptionLabel("", "allow_always", "x"), Is.EqualTo("Always allow"));
        Assert.That(AgentEventPresentationProvider.OptionLabel("", "reject_once", "x"), Is.EqualTo("Reject"));
        Assert.That(AgentEventPresentationProvider.OptionLabel("", "reject_always", "x"), Is.EqualTo("Always reject"));
        Assert.That(AgentEventPresentationProvider.OptionLabel("", "other", "opt-9"), Is.EqualTo("opt-9"));
    }

    [Test]
    public void Present_formatsPlansCompactionAndLargeUsage()
    {
        var plan = Present("""{"sessionUpdate":"plan_update","entries":[{"content":"One","status":"completed"},{"content":"Two","status":"in_progress"},{"content":"Three","status":"pending"}]}""");
        Assert.That(plan.Kind, Is.EqualTo("plan"));
        Assert.That(plan.Text, Does.Contain("✓ One"));
        Assert.That(Present("""{"sessionUpdate":"session_info_update"}""").Kind, Is.EqualTo("ignore"));
        Assert.That(Present("""{"sessionUpdate":"compaction_start"}""").Compacting, Is.True);
        Assert.That(Present("""{"sessionUpdate":"compaction_completed_summary"}""").Compacting, Is.False);
        Assert.That(Present("""{"sessionUpdate":"usage_update","used":1500000,"size":2000000}""").Usage, Is.EqualTo("1.5M / 2.0M"));
        Assert.That(Present("""{"sessionUpdate":"usage_update","used":1500,"size":1800}""").Usage, Is.EqualTo("1.5k / 1.8k"));
        Assert.That(Present("""{"sessionUpdate":"usage_update","used":12,"size":20}""").Usage, Is.EqualTo("12 / 20"));
        Assert.That(Present("""{"sessionUpdate":"agent_thought_chunk","content":{"text":""}}""").Kind, Is.EqualTo("ignore"));
        Assert.That(Present("""{"sessionUpdate":"agent_message_chunk","content":[{"text":"Hello"},{"text":"world"}]}""").Text, Is.EqualTo("Hello\nworld"));
        Assert.That(Parse("[]"), Is.Null);
        Assert.That(Parse("""{"requestId":"req"}"""), Is.Null);
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
