using AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

namespace AgentUp.Capabilities.Cursor.Tests.Features.CursorCapability.Unit;

[TestFixture]
public sealed class CursorAgentCapabilityTests
{
    [Test]
    public void Launch_uses_the_cursor_acp_stdio_command()
    {
        var launch = new CursorAgentCapability().Launch(new Dictionary<string, string>());

        Assert.That(launch.FileName, Is.EqualTo("agent"));
        Assert.That(launch.Arguments, Is.EqualTo(new[] { "acp" }));
    }

    [Test]
    public void Login_uses_poll_transport()
    {
        Assert.That(new CursorAgentCapability().Login!.Transport, Is.EqualTo("poll"));
    }

    [Test]
    public void SupportsPermissionRequests_isTrue()
    {
        Assert.That(new CursorAgentCapability().SupportsPermissionRequests, Is.True);
    }

    [Test]
    public void Software_needs_nix_for_node_and_the_acp_adapter_on_the_search_path()
    {
        var software = new CursorAgentCapability().Software;

        Assert.That(software.Single(item => item.DeliveredByNix).Executable, Is.EqualTo("node"));
        Assert.That(software.Single(item => !item.DeliveredByNix).Executable, Is.EqualTo("agent"));
    }
}
