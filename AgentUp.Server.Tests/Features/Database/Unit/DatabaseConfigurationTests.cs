using System.Text.Json;
using AgentUp.Server.Features.Orchestration.DTOs;

namespace AgentUp.Server.Tests.Features.Database.Unit;

[TestFixture]
public sealed class DatabaseConfigurationTests
{
    [Test]
    public void Application_databaseFlag_deserializes()
    {
        var configuration = JsonSerializer.Deserialize<AgentUpConfiguration>("""{"name":"demo","applications":[{"name":"db","command":"postgres","database":true}]}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.That(configuration!.Applications!.Single().Database, Is.True);
    }
}
