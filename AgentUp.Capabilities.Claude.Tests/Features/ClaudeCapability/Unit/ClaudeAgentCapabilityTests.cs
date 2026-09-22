using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;

namespace AgentUp.Capabilities.Claude.Tests.Features.ClaudeCapability.Unit;

[TestFixture]
public sealed class ClaudeAgentCapabilityTests
{
    [Test]
    public void Launch_uses_the_claude_acp_stdio_command()
    {
        var launch = new ClaudeAgentCapability().Launch();

        Assert.That(launch.FileName, Is.EqualTo("claude-agent-acp"));
        Assert.That(launch.Arguments, Is.Empty);
    }

    [Test]
    public void Login_uses_pasted_code_transport()
    {
        Assert.That(new ClaudeAgentCapability().Login!.Transport, Is.EqualTo("code"));
    }
}
