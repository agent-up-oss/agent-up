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

    [Test]
    public void Read_fallsBackToTheExpoPublicUrlAndDefaultName()
    {
        var recommended = RecommendedServerProvider.Read(new Dictionary<string, string?>
        {
            ["EXPO_PUBLIC_RECOMMENDED_SERVER_URL"] = "http://127.0.0.1:5288",
            ["EXPO_PUBLIC_RECOMMENDED_SERVER_NAME"] = "  "
        });

        Assert.Multiple(() =>
        {
            Assert.That(recommended, Is.Not.Null);
            Assert.That(recommended!.Url, Is.EqualTo("http://127.0.0.1:5288"));
            Assert.That(recommended.DisplayName, Is.EqualTo(RecommendedServer.DefaultDisplayName));
        });
    }

    [Test]
    public void Read_returnsNullWhenTheUrlIsNotAUsableServerAddress()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                RecommendedServerProvider.Read(new Dictionary<string, string?>
                {
                    ["AGENTUP_RECOMMENDED_SERVER_URL"] = "not-a-url"
                }),
                Is.Null);
            Assert.That(
                RecommendedServerProvider.Read(new Dictionary<string, string?>
                {
                    ["AGENTUP_RECOMMENDED_SERVER_URL"] = "http://example.com"
                }),
                Is.Null);
            Assert.That(
                RecommendedServerProvider.Read(new Dictionary<string, string?>
                {
                    ["AGENTUP_RECOMMENDED_SERVER_URL"] = "  "
                }),
                Is.Null);
        });
    }

    [Test]
    public void Read_usesProcessEnvironmentWhenNoOverrideIsPassed()
    {
        var originalUrl = Environment.GetEnvironmentVariable("AGENTUP_RECOMMENDED_SERVER_URL");
        var originalName = Environment.GetEnvironmentVariable("AGENTUP_RECOMMENDED_SERVER_NAME");
        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_RECOMMENDED_SERVER_URL", "http://127.0.0.1:6001");
            Environment.SetEnvironmentVariable("AGENTUP_RECOMMENDED_SERVER_NAME", "Lab Server");

            var recommended = RecommendedServerProvider.Read();

            Assert.Multiple(() =>
            {
                Assert.That(recommended, Is.Not.Null);
                Assert.That(recommended!.Url, Is.EqualTo("http://127.0.0.1:6001"));
                Assert.That(recommended.DisplayName, Is.EqualTo("Lab Server"));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTUP_RECOMMENDED_SERVER_URL", originalUrl);
            Environment.SetEnvironmentVariable("AGENTUP_RECOMMENDED_SERVER_NAME", originalName);
        }
    }
}
