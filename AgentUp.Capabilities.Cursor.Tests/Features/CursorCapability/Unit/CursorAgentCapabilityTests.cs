using AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

namespace AgentUp.Capabilities.Cursor.Tests.Features.CursorCapability.Unit;

[TestFixture]
public sealed class CursorAgentCapabilityTests
{
    [Test]
    public void Launch_uses_the_cursor_acp_stdio_command()
    {
        var launch = new CursorAgentCapability().Launch();

        Assert.That(launch.FileName, Is.EqualTo("agent"));
        Assert.That(launch.Arguments, Is.EqualTo(new[] { "acp" }));
    }

    [Test]
    public void Login_uses_poll_transport()
    {
        Assert.That(new CursorAgentCapability().Login!.Transport, Is.EqualTo("poll"));
    }
}
