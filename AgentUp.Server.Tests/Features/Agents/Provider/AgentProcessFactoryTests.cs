using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentProcessFactoryTests
{
    [Test]
    public void Create_returnsAnAcpProcessProvider()
    {
        var factory = new AgentProcessFactory(
            new AgentCommandProvider(new ConfigurationBuilder().Build(), []),
            NullLoggerFactory.Instance);

        Assert.That(factory.Create(), Is.TypeOf<AcpProcessProvider>());
    }
}
