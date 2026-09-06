using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Database.Providers;
using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Tests.Features.Database.Provider;

[TestFixture]
public sealed class PostgresDatabaseAdapterTests
{
    [Test]
    public void CanHandle_requiresDatabaseFlagAndPostgresPort()
    {
        var adapter = new PostgresDatabaseAdapter();
        var application = new ApplicationInstance
        {
            Name = "postgres",
            Database = true,
            AllocatedPorts = [new PortMapping("POSTGRES_PORT", 5432, 12000, "tcp")]
        };

        Assert.That(adapter.CanHandle(application), Is.True);
        Assert.That(adapter.CanHandle(new ApplicationInstance { Name = "other", Database = true }), Is.False);
        Assert.That(adapter.CanHandle(new ApplicationInstance { Name = "postgres", AllocatedPorts = application.AllocatedPorts }), Is.False);
    }
}
