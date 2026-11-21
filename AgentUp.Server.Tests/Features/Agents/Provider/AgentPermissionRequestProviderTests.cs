using AgentUp.Server.Tests.Support;
using System.Collections.Concurrent;
using System.Text.Json;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentPermissionRequestProviderTests
{
    [Test]
    public void WithAnnouncedToolCall_givesTheRequestTheCommandTheAgentAlreadyAnnounced()
    {
        var calls = new ConcurrentDictionary<string, JsonElement>
        {
            ["call-1"] = Json("""{"sessionUpdate":"tool_call_update","toolCallId":"call-1","kind":"execute","title":"git diff","rawInput":{"command":"git diff","workdir":"/repo"}}""")
        };
        // What OpenCode sends when it asks: the generic title and no command.
        var request = Json("""{"options":[{"optionId":"once","kind":"allow_once"}],"toolCall":{"toolCallId":"call-1","kind":"execute","title":"bash","rawInput":{"cwd":"/repo"}}}""");

        var completed = AgentPermissionRequestProvider.WithAnnouncedToolCall(request, calls);
        var decision = new DeclaredPermissionPolicy([new PermissionRuleBuilder("review-git", PermissionRuleKind.Execute).Scoped("git").Build()]).Decide(completed);

        Assert.Multiple(() =>
        {
            Assert.That(completed.GetProperty("toolCall").GetProperty("rawInput").GetProperty("command").GetString(), Is.EqualTo("git diff"));
            Assert.That(completed.GetProperty("toolCall").GetProperty("title").GetString(), Is.EqualTo("bash"), "what the request says is kept");
            Assert.That(decision!.RuleId, Is.EqualTo("review-git"));
        });
    }

    [Test]
    public void WithAnnouncedToolCall_leavesARequestAloneWhenNothingWasAnnounced()
    {
        var request = Json("""{"toolCall":{"toolCallId":"call-9","kind":"execute","title":"bash"}}""");

        var completed = AgentPermissionRequestProvider.WithAnnouncedToolCall(request, new ConcurrentDictionary<string, JsonElement>());

        Assert.That(completed.GetRawText(), Is.EqualTo(request.GetRawText()));
    }

    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);
}
