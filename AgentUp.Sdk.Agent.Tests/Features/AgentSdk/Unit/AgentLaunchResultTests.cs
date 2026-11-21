using AgentUp.Sdk.Agent;

namespace AgentUp.Sdk.Agent.Tests.Features.AgentSdk.Unit;

[TestFixture]
public sealed class AgentLaunchResultTests
{
    [Test]
    public void A_launch_without_environment_adds_no_variables()
    {
        var launch = new AgentLaunchResult("claude-agent-acp", []);

        Assert.That(launch.Environment, Is.Null);
        Assert.That(launch.NixPackage, Is.Null);
    }

    [Test]
    public void A_launch_carries_its_environment_apart_from_its_arguments()
    {
        var launch = new AgentLaunchResult(
            "opencode",
            ["acp"],
            "opencode",
            new Dictionary<string, string> { ["SECRET"] = "value" });

        Assert.That(launch.Arguments, Is.EqualTo(new[] { "acp" }));
        Assert.That(launch.Environment!["SECRET"], Is.EqualTo("value"));
    }
}
