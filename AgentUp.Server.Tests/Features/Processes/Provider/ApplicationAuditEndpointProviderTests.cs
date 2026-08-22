using AgentUp.Server.Features.Processes.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Processes.Provider;

[TestFixture]
public sealed class ApplicationAuditEndpointProviderTests
{
    [Test]
    public void GetRecordEndpoint_UsesTheServerConfiguredDevelopmentUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["urls"] = "http://localhost:5001" })
            .Build();

        var endpoint = new ApplicationAuditEndpointProvider(configuration).GetRecordEndpoint();

        Assert.That(endpoint, Is.EqualTo("http://localhost:5001/api/audit/record"));
    }

    [Test]
    public void GetRecordEndpoint_UsesPackagedServerDefault()
    {
        var endpoint = new ApplicationAuditEndpointProvider(new ConfigurationBuilder().Build()).GetRecordEndpoint();

        Assert.That(endpoint, Is.EqualTo("http://127.0.0.1:5000/api/audit/record"));
    }

    [TestCase("")]
    [TestCase(";")]
    [TestCase(" ; ")]
    public void GetRecordEndpoint_UsesPackagedDefaultForEmptyConfiguredUrls(string configured)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["urls"] = configured })
            .Build();

        Assert.That(
            new ApplicationAuditEndpointProvider(configuration).GetRecordEndpoint(),
            Is.EqualTo("http://127.0.0.1:5000/api/audit/record"));
    }
}
