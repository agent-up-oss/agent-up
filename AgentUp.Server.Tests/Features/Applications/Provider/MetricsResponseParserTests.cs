using AgentUp.Server.Features.Applications.Providers;

namespace AgentUp.Server.Tests.Features.Applications.Provider;

[TestFixture]
public sealed class MetricsResponseParserTests
{
    [Test]
    public void Parse_FlattensNestedMetricsObject()
    {
        const string body = """
            {
              "metrics": {
                "requests_total": 42,
                "memory_bytes": "1048576"
              }
            }
            """;

        var result = MetricsResponseParser.Parse(body);

        Assert.Multiple(() =>
        {
            Assert.That(result["metric.requests_total"], Is.EqualTo("42"));
            Assert.That(result["metric.memory_bytes"], Is.EqualTo("1048576"));
        });
    }

    [Test]
    public void Parse_FlattensTopLevelObject_WhenMetricsPropertyMissing()
    {
        const string body = """
            {
              "queue_depth": 3,
              "status": "ok"
            }
            """;

        var result = MetricsResponseParser.Parse(body);

        Assert.Multiple(() =>
        {
            Assert.That(result["metric.queue_depth"], Is.EqualTo("3"));
            Assert.That(result["metric.status"], Is.EqualTo("ok"));
        });
    }

    [Test]
    public void Parse_ReturnsEmptyDictionary_ForInvalidJson()
    {
        Assert.That(MetricsResponseParser.Parse("{not-json"), Is.Empty);
    }
}
