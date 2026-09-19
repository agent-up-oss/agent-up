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
    public void RunSummary_namesToolsAndThoughts()
    {
        Assert.That(AgentEventPresentationProvider.RunSummary([]), Is.EqualTo("Worked"));
        Assert.That(AgentEventPresentationProvider.RunSummary(["Agent"]), Is.EqualTo("Worked"));
        Assert.That(AgentEventPresentationProvider.RunSummary(["Thought", "Tool", "Tool", "Agent"]), Is.EqualTo("Worked · 2 tools · Thought"));
        Assert.That(AgentEventPresentationProvider.RunSummary(["Tool"]), Is.EqualTo("Worked · 1 tool"));
    }

    [Test]
    public void VisibleText_stripsMarkdownMarkers()
    {
        Assert.That(AgentEventPresentationProvider.VisibleText("**Clarifying test meaning**"), Is.EqualTo("Clarifying test meaning"));
        Assert.That(AgentEventPresentationProvider.VisibleText("Use `cargo test`"), Is.EqualTo("Use cargo test"));
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

    // While a run is going the label names whatever the agent last did, so each update kind
    // is its own case rather than one line in a block of thirteen.
    [TestCase("Thought", null, "Thinking")]
    [TestCase("Tool", "Read file", "Using Read file")]
    [TestCase("Tool", null, "Using a tool")]
    [TestCase("Agent", null, "Writing")]
    [TestCase("Plan", null, "Working through the plan")]
    [TestCase("Compacting", null, "Compacting context")]
    [TestCase(null, null, "Working")]
    public void ActivityLabel_namesTheLatestUpdateWhileRunning(string? kind, string? detail, string expected)
        => Assert.That(
            AgentEventPresentationProvider.ActivityLabel("running", false, null, kind, detail),
            Is.EqualTo(expected));

    [TestCase("ready", "Idle")]
    [TestCase("stopped", "Stopped")]
    [TestCase("authentication_required", "Waiting for sign-in")]
    [TestCase("authenticating", "Waiting for sign-in")]
    public void ActivityLabel_namesTheSessionStateWhenNothingIsRunning(string state, string expected)
        => Assert.That(
            AgentEventPresentationProvider.ActivityLabel(state, false, null, null, null),
            Is.EqualTo(expected));

    [Test]
    public void ActivityLabel_saysADecisionIsWaitedOn_aheadOfWhateverTheAgentWasDoing()
        => Assert.That(
            AgentEventPresentationProvider.ActivityLabel("running", true, null, "Thought", null),
            Is.EqualTo("Waiting for a decision"));

    [Test]
    public void ActivityLabel_showsAnError_aheadOfTheSessionState()
        => Assert.That(
            AgentEventPresentationProvider.ActivityLabel("ready", false, "boom", null, null),
            Is.EqualTo("boom"));

    [Test]
    public void OptionLabel_fallsBackToKindThenOptionId()
    {
        Assert.That(AgentEventPresentationProvider.OptionLabel("", "allow_always", "x"), Is.EqualTo("Always allow"));
        Assert.That(AgentEventPresentationProvider.OptionLabel("", "reject_once", "x"), Is.EqualTo("Reject"));
        Assert.That(AgentEventPresentationProvider.OptionLabel("", "reject_always", "x"), Is.EqualTo("Always reject"));
        Assert.That(AgentEventPresentationProvider.OptionLabel("", "other", "opt-9"), Is.EqualTo("opt-9"));
    }

    [Test]
    public void Present_rendersAPlanWithItsCompletedEntriesTicked()
    {
        var plan = Present("""
            {"sessionUpdate":"plan_update","entries":[{"content":"One","status":"completed"},
            {"content":"Two","status":"in_progress"},{"content":"Three","status":"pending"}]}
            """);

        Assert.That(plan.Kind, Is.EqualTo("plan"));
        Assert.That(plan.Text, Does.Contain("✓ One"));
    }

    [TestCase("compaction_start", true)]
    [TestCase("compaction_completed_summary", false)]
    public void Present_tracksWhetherTheAgentIsCompactingItsContext(string update, bool compacting)
        => Assert.That(Present($$"""{"sessionUpdate":"{{update}}"}""").Compacting, Is.EqualTo(compacting));

    // Context usage is read at a glance, so it is scaled rather than printed in full.
    [TestCase(1500000, 2000000, "1.5M / 2.0M")]
    [TestCase(1500, 1800, "1.5k / 1.8k")]
    [TestCase(12, 20, "12 / 20")]
    public void Present_scalesContextUsageToTheSizeItReports(int used, int size, string expected)
        => Assert.That(
            Present($$"""{"sessionUpdate":"usage_update","used":{{used}},"size":{{size}}}""").Usage,
            Is.EqualTo(expected));

    [TestCase("""{"sessionUpdate":"session_info_update"}""")]
    [TestCase("""{"sessionUpdate":"agent_thought_chunk","content":{"text":""}}""")]
    public void Present_ignoresUpdatesWithNothingToShow(string json)
        => Assert.That(Present(json).Kind, Is.EqualTo("ignore"));

    [Test]
    public void Present_joinsTheChunksOfAnAgentMessage()
        => Assert.That(
            Present("""{"sessionUpdate":"agent_message_chunk","content":[{"text":"Hello"},{"text":"world"}]}""").Text,
            Is.EqualTo("Hello\nworld"));

    [TestCase("[]")]
    [TestCase("""{"requestId":"req"}""")]
    public void ParsePermission_returnsNothingForAPayloadThatIsNotAPrompt(string json)
        => Assert.That(Parse(json), Is.Null);

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
