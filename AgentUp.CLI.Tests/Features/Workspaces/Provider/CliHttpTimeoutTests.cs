using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class CliHttpTimeoutTests
{
    [Test]
    public void Server_timeout_outlives_a_cold_workspace_start()
    {
        Assert.That(CliHttpTimeout.Server, Is.EqualTo(TimeSpan.FromMinutes(20)));
    }

    [Test]
    public void Server_timeout_is_longer_than_the_HttpClient_default()
    {
        Assert.That(CliHttpTimeout.Server, Is.GreaterThan(TimeSpan.FromSeconds(100)));
    }
}
