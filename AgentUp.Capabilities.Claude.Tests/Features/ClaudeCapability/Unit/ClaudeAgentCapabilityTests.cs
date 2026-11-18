using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;

namespace AgentUp.Capabilities.Claude.Tests.Features.ClaudeCapability.Unit;

[TestFixture]
public sealed class ClaudeAgentCapabilityTests
{
    [Test]
    public void Launch_uses_the_claude_acp_stdio_command()
    {
        var launch = new ClaudeAgentCapability().Launch(new Dictionary<string, string>());

        Assert.That(launch.FileName, Is.EqualTo("claude-agent-acp"));
        Assert.That(launch.Arguments, Is.Empty);
    }

    [Test]
    public void Login_uses_pasted_code_transport()
    {
        Assert.That(new ClaudeAgentCapability().Login!.Transport, Is.EqualTo("code"));
    }

    [Test]
    public void SupportsPermissionRequests_isTrue()
    {
        Assert.That(new ClaudeAgentCapability().SupportsPermissionRequests, Is.True);
    }

    [Test]
    public void Software_needs_nix_for_node_and_the_acp_adapter_on_the_search_path()
    {
        var software = new ClaudeAgentCapability().Software;

        Assert.That(software.Single(item => item.DeliveredByNix).Executable, Is.EqualTo("node"));
        Assert.That(software.Single(item => !item.DeliveredByNix).Executable, Is.EqualTo("claude-agent-acp"));
    }
}
