using AgentUp.Desktop.Features.Authentication.Models;
using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class RecommendedServerProviderTests
{
    [Test]
    public void Read_returnsTheConfiguredRecommendedServer()
    {
        var recommended = RecommendedServerProvider.Read(new Dictionary<string, string?>
        {
            ["AGENTUP_RECOMMENDED_SERVER_URL"] = "http://127.0.0.1:5288/",
            ["AGENTUP_RECOMMENDED_SERVER_NAME"] = "Agent-Up Cloud"
        });

        Assert.Multiple(() =>
        {
            Assert.That(recommended, Is.Not.Null);
            Assert.That(recommended!.Url, Is.EqualTo("http://127.0.0.1:5288"));
            Assert.That(recommended.DisplayName, Is.EqualTo("Agent-Up Cloud"));
            Assert.That(recommended.Id, Is.EqualTo(RecommendedServer.RecommendedId));
        });
    }

    [Test]
    public void Read_returnsNullWhenTheUrlIsMissing()
    {
        Assert.That(RecommendedServerProvider.Read(new Dictionary<string, string?>()), Is.Null);
    }
}
