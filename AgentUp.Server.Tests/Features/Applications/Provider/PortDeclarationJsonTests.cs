using System.Text.Json;
using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Tests.Features.Applications.Provider;

[TestFixture]
public sealed class PortDeclarationJsonTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public void Deserialize_UsesHealthCheckAndMetricsJsonKeys()
    {
        const string json = """
            {
              "variable": "WEB_PORT",
              "defaultPort": 3000,
              "protocol": "http",
              "healthCheck": "/health",
              "metrics": "/metrics"
            }
            """;

        var port = JsonSerializer.Deserialize<PortDeclaration>(json, JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(port, Is.Not.Null);
            Assert.That(port!.HealthCheckPath, Is.EqualTo("/health"));
            Assert.That(port.MetricsPath, Is.EqualTo("/metrics"));
        });
    }
}
