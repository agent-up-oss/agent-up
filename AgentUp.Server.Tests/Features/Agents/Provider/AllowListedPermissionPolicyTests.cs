using System.Text.Json;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AllowListedPermissionPolicyTests
{
    [Test]
    public void Decide_returnsTheRecordedRuleWhenTheOptionExists()
    {
        var request = JsonSerializer.SerializeToElement(new
        {
            options = new[] { new { optionId = "allow-once" }, new { optionId = "reject-once" } }
        });

        var decision = new AllowListedPermissionPolicy("allow-once", "allow-all").Decide(request);

        Assert.Multiple(() =>
        {
            Assert.That(decision!.OptionId, Is.EqualTo("allow-once"));
            Assert.That(decision.RuleId, Is.EqualTo("allow-all"));
        });
    }

    [Test]
    public void Decide_returnsNullWhenTheOptionIsNotOffered()
    {
        var request = JsonSerializer.SerializeToElement(new
        {
            options = new[] { new { optionId = "reject-once" } }
        });

        var decision = new AllowListedPermissionPolicy("allow-once", "allow-all").Decide(request);

        Assert.That(decision, Is.Null);
    }

    [Test]
    public void Decide_returnsNullWhenOptionsAreMissing()
    {
        var request = JsonSerializer.SerializeToElement(new { reason = "no options" });

        Assert.That(new AllowListedPermissionPolicy("allow-once", "allow-all").Decide(request), Is.Null);
    }
}
