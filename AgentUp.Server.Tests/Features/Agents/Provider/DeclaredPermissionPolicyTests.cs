using AgentUp.Server.Tests.Support;
using System.Text.Json;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class DeclaredPermissionPolicyTests
{
    [Test]
    public void Decide_allowsAMatchingWriteWithThePreferredAllowOption()
    {
        var policy = new DeclaredPermissionPolicy([
            new PermissionRuleBuilder("write-src", PermissionRuleKind.WorktreeWrite).Scoped("src/").Build()
        ]);

        var decision = policy.Decide(Request(kind: "edit", title: "Write file", path: "src/app.ts"));

        Assert.Multiple(() =>
        {
            Assert.That(decision!.OptionId, Is.EqualTo("allow-once"));
            Assert.That(decision.RuleId, Is.EqualTo("write-src"));
        });
    }

    [Test]
    public void Decide_returnsNullWhenNoRuleMatches()
    {
        var policy = new DeclaredPermissionPolicy([
            new PermissionRuleBuilder("read", PermissionRuleKind.WorktreeRead).Build()
        ]);

        Assert.That(policy.Decide(Request(kind: "edit", title: "Write file", path: "src/app.ts")), Is.Null);
    }

    [Test]
    public void Decide_returnsNullWhenTheMatchingRuleRefuses()
    {
        var policy = new DeclaredPermissionPolicy([
            new PermissionRuleBuilder("deny-net", PermissionRuleKind.Network).Denied().Build()
        ]);

        Assert.That(policy.Decide(Request(kind: "network", title: "fetch https://example", path: "")), Is.Null);
    }

    [Test]
    public void Classify_mapsReadExecuteAndNetworkFromToolMetadata()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DeclaredPermissionPolicy.Classify(Request("read", "Read file", "README.md")), Is.EqualTo(PermissionRuleKind.WorktreeRead));
            Assert.That(DeclaredPermissionPolicy.Classify(Request("execute", "bash ls", "")), Is.EqualTo(PermissionRuleKind.Execute));
            Assert.That(DeclaredPermissionPolicy.Classify(Request("tool", "HTTP GET", "")), Is.EqualTo(PermissionRuleKind.Network));
        });
    }

    [Test]
    public void Decide_scopesExecuteToTheDeclaredCommandPrefix()
    {
        var policy = new DeclaredPermissionPolicy([
            new PermissionRuleBuilder("dotnet", PermissionRuleKind.Execute).Scoped("dotnet").Build()
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(policy.Decide(Request("execute", "dotnet test", "", "dotnet test"))!.RuleId, Is.EqualTo("dotnet"));
            Assert.That(policy.Decide(Request("execute", "bash", "", "bash")), Is.Null);
        });
    }

    [Test]
    public void Decide_readsTheCommandAnAgentSendsInRawInput()
    {
        var policy = new DeclaredPermissionPolicy([
            new PermissionRuleBuilder("review-git", PermissionRuleKind.Execute).Scoped("git").Build()
        ]);
        // OpenCode titles a shell call "bash" and puts the command in rawInput.
        var gitDiff = JsonSerializer.Deserialize<JsonElement>("""
            {"options":[{"optionId":"once","kind":"allow_once"}],"toolCall":{"kind":"execute","title":"bash","rawInput":{"command":"git diff","workdir":"/repo"}}}
            """);
        var remove = JsonSerializer.Deserialize<JsonElement>("""
            {"options":[{"optionId":"once","kind":"allow_once"}],"toolCall":{"kind":"execute","title":"bash","rawInput":{"command":"rm -rf src"}}}
            """);

        Assert.Multiple(() =>
        {
            Assert.That(policy.Decide(gitDiff)!.RuleId, Is.EqualTo("review-git"));
            Assert.That(policy.Decide(remove), Is.Null, "a scoped rule allows only its own command");
        });
    }

    // OpenCode labels its shell call "other"; it is still a command, not a write to the clone.
    [Test]
    public void Decide_treatsACallCarryingACommandAsExecuteWhateverItsKind()
    {
        var policy = new DeclaredPermissionPolicy([
            new PermissionRuleBuilder("review-git", PermissionRuleKind.Execute).Scoped("git").Build()
        ]);
        var gitDiff = JsonSerializer.Deserialize<JsonElement>("""
            {"options":[{"optionId":"once","kind":"allow_once"}],"toolCall":{"kind":"other","title":"git diff --name-only src/greeting.js","rawInput":{"command":"git diff --name-only src/greeting.js"}}}
            """);

        Assert.Multiple(() =>
        {
            Assert.That(DeclaredPermissionPolicy.Classify(gitDiff), Is.EqualTo(PermissionRuleKind.Execute));
            Assert.That(policy.Decide(gitDiff)!.RuleId, Is.EqualTo("review-git"));
        });
    }

    [Test]
    public void Decide_returnsNullWhenOptionsAreMissing()
    {
        var policy = new DeclaredPermissionPolicy([
            new PermissionRuleBuilder("write", PermissionRuleKind.WorktreeWrite).Build()
        ]);

        var decision = policy.Decide(JsonSerializer.Deserialize<JsonElement>("""{"toolCall":{"kind":"edit","title":"Write"}}"""));

        Assert.That(decision!.OptionId, Is.Empty);
    }

    [Test]
    public void Decide_readsAStringLocationAndANetworkScope()
    {
        var write = new DeclaredPermissionPolicy([
            new PermissionRuleBuilder("write-src", PermissionRuleKind.WorktreeWrite).Scoped("src/").Build()
        ]);
        var network = new DeclaredPermissionPolicy([
            new PermissionRuleBuilder("net", PermissionRuleKind.Network).Scoped("https://").Build()
        ]);
        var stringLocation = JsonSerializer.Deserialize<JsonElement>("""
            {"options":[{"optionId":"allow-once"}],"toolCall":{"kind":"edit","title":"Write","locations":["src/app.ts"]}}
            """);
        var fetch = Request("network", "fetch https://example", "");

        Assert.Multiple(() =>
        {
            Assert.That(write.Decide(stringLocation)!.RuleId, Is.EqualTo("write-src"));
            Assert.That(network.Decide(fetch)!.RuleId, Is.EqualTo("net"));
            Assert.That(DeclaredPermissionPolicy.Classify(JsonSerializer.Deserialize<JsonElement>("{}")), Is.EqualTo(PermissionRuleKind.WorktreeWrite));
        });
    }

    [Test]
    public void Classify_treatsTheAcpFetchKindAsNetworkEvenWhenTheTitleMentionsReading()
    {
        var policy = new DeclaredPermissionPolicy([
            new PermissionRuleBuilder("browser", PermissionRuleKind.Network).Build()
        ]);

        var decision = policy.Decide(Request("fetch", "fetch read_page", ""));

        Assert.That(decision!.RuleId, Is.EqualTo("browser"));
    }

    private static JsonElement Request(string kind, string title, string path, string? command = null)
    {
        var json = JsonSerializer.Serialize(new
        {
            options = new[]
            {
                new { optionId = "allow-once", kind = "allow_once" },
                new { optionId = "reject-once", kind = "reject_once" }
            },
            toolCall = new { kind, title, command, locations = new[] { new { path } } }
        });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
