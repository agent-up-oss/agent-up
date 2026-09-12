using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Abstractions.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityLaunchPlanTests
{
    [Test]
    public void Arguments_areOptionalForLegacyCommandStrings()
    {
        var plan = new CapabilityLaunchPlan(
            "docker run postgres:17",
            WorkingDirectory: "/repo",
            Environment: new Dictionary<string, string> { ["WEB_PORT"] = "1" });

        Assert.Multiple(() =>
        {
            Assert.That(plan.Command, Is.EqualTo("docker run postgres:17"));
            Assert.That(plan.WorkingDirectory, Is.EqualTo("/repo"));
            Assert.That(plan.Environment!["WEB_PORT"], Is.EqualTo("1"));
            Assert.That(plan.Arguments, Is.Null);
        });
    }

    [Test]
    public void Arguments_carryAcpExecutableArgumentList()
    {
        var plan = new CapabilityLaunchPlan("/home/dev/.local/bin/agent", Arguments: ["acp"]);

        Assert.That(plan.Arguments, Is.EqualTo(new[] { "acp" }));
    }
}
