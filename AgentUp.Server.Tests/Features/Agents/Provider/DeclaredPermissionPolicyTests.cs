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
            new PermissionRule("write-src", PermissionRuleKind.WorktreeWrite, true, "src/")
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
            new PermissionRule("read", PermissionRuleKind.WorktreeRead, true)
        ]);

        Assert.That(policy.Decide(Request(kind: "edit", title: "Write file", path: "src/app.ts")), Is.Null);
    }

    [Test]
    public void Decide_returnsNullWhenTheMatchingRuleRefuses()
    {
        var policy = new DeclaredPermissionPolicy([
            new PermissionRule("deny-net", PermissionRuleKind.Network, false)
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
            new PermissionRule("dotnet", PermissionRuleKind.Execute, true, "dotnet")
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(policy.Decide(Request("execute", "dotnet test", "", "dotnet test"))!.RuleId, Is.EqualTo("dotnet"));
            Assert.That(policy.Decide(Request("execute", "bash", "", "bash")), Is.Null);
        });
    }

    [Test]
    public void Decide_returnsNullWhenOptionsAreMissing()
    {
        var policy = new DeclaredPermissionPolicy([
            new PermissionRule("write", PermissionRuleKind.WorktreeWrite, true)
        ]);

        var decision = policy.Decide(JsonSerializer.Deserialize<JsonElement>("""{"toolCall":{"kind":"edit","title":"Write"}}"""));

        Assert.That(decision!.OptionId, Is.Empty);
    }

    [Test]
    public void Decide_readsAStringLocationAndANetworkScope()
    {
        var write = new DeclaredPermissionPolicy([
            new PermissionRule("write-src", PermissionRuleKind.WorktreeWrite, true, "src/")
        ]);
        var network = new DeclaredPermissionPolicy([
            new PermissionRule("net", PermissionRuleKind.Network, true, "https://")
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
