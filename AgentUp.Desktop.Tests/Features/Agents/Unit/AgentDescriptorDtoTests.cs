using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentDescriptorDtoTests
{
    [Test]
    public void StatusLabel_isAvailableWhenTheAgentCanRun()
    {
        var agent = DesktopDomain.AvailableAgent("claude", "Claude");

        Assert.Multiple(() =>
        {
            Assert.That(agent.StatusLabel, Is.EqualTo("Available"));
            Assert.That(agent.CanEnable, Is.False);
            Assert.That(agent.IsBlocked, Is.False);
            Assert.That(agent.HasDetail, Is.False);
            Assert.That(agent.HasRemedy, Is.False);
        });
    }

    [Test]
    public void StatusLabel_isNotEnabledWhenThePackageIsTheGap()
    {
        var agent = DesktopDomain.Agent().Named("claude", "Claude").PackageDisabled().Build();

        Assert.Multiple(() =>
        {
            Assert.That(agent.StatusLabel, Is.EqualTo("Not enabled"));
            Assert.That(agent.CanEnable, Is.True);
            Assert.That(agent.IsBlocked, Is.False);
            Assert.That(agent.HasDetail, Is.True);
            Assert.That(agent.HasRemedy, Is.True);
        });
    }

    [Test]
    public void StatusLabel_isExecutableNotFoundWhenTheCliIsMissing()
    {
        var agent = DesktopDomain.Agent().Named("claude", "Claude").ExecutableMissing("claude-agent-acp").Build();

        Assert.Multiple(() =>
        {
            Assert.That(agent.StatusLabel, Is.EqualTo("Executable not found"));
            Assert.That(agent.CanEnable, Is.False);
            Assert.That(agent.IsBlocked, Is.True);
            Assert.That(agent.HasDetail, Is.True);
            Assert.That(agent.HasRemedy, Is.True);
        });
    }

    [Test]
    public void HasDetailAndHasRemedy_ignoreBlankCopy()
    {
        var agent = DesktopDomain.Agent().Named("claude", "Claude").PackageDisabled().Build()
            with { Detail = "  ", Remedy = null };

        Assert.That(agent.HasDetail, Is.False);
        Assert.That(agent.HasRemedy, Is.False);
    }
}
