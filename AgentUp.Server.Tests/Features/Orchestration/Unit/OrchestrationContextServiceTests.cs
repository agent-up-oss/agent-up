using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Services;

namespace AgentUp.Server.Tests.Features.Orchestration.Unit;

[TestFixture]
public sealed class OrchestrationContextServiceTests
{
    [Test]
    public void GetAgentUpContext_ReturnsProviderContext()
    {
        var service = new OrchestrationContextService(new FakeContextProvider());

        var context = service.GetAgentUpContext();

        Assert.That(context, Is.EqualTo("context"));
    }

    [Test]
    public void GetAgentUpJsonFormat_ReturnsProviderFormat()
    {
        var service = new OrchestrationContextService(new FakeContextProvider());

        var format = service.GetAgentUpJsonFormat();

        Assert.That(format, Is.EqualTo("format"));
    }

    private sealed class FakeContextProvider : IAgentUpContextProvider
    {
        public string GetAgentUpContext() => "context";

        public string GetAgentUpJsonFormat() => "format";
    }
}
