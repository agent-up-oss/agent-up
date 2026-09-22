using AgentUp.Capabilities.Codex.Features.CodexCapability.Services;

namespace AgentUp.Capabilities.Codex.Tests.Features.CodexCapability.Unit;

[TestFixture]
public sealed class CodexAgentCapabilityTests
{
    [Test]
    public void Launch_uses_the_codex_acp_stdio_command()
    {
        var launch = new CodexAgentCapability().Launch();

        Assert.That(launch.FileName, Is.EqualTo("codex-acp"));
        Assert.That(launch.Arguments, Is.Empty);
    }

    [Test]
    public void Login_uses_device_code_transport()
    {
        var login = new CodexAgentCapability().Login;

        Assert.That(login!.FileName, Is.EqualTo("codex"));
        Assert.That(login.Transport, Is.EqualTo("code"));
    }
}
