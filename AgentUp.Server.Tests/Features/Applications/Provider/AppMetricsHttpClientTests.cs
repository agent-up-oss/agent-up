using AgentUp.Server.Features.Applications.Providers;

namespace AgentUp.Server.Tests.Features.Applications.Provider;

[TestFixture]
public sealed class AppMetricsHttpClientTests
{
    [TestCase("@attacker.example/")]
    [TestCase("//attacker.example/")]
    [TestCase("not-a-path")]
    [TestCase("/..\\escape")]
    public async Task FetchAsync_RejectsPathsThatCouldChangeRequestAuthority(string maliciousPath)
    {
        using var client = new AppMetricsHttpClient();

        var result = await client.FetchAsync(65_535, maliciousPath, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void FetchAsync_AcceptsOriginRelativePath_AndAttemptsTheRequest()
    {
        using var client = new AppMetricsHttpClient();

        // Nothing is listening on this port. A malicious path is rejected before any
        // request is attempted (see above), so this well-formed path must instead fail
        // with a connection error, proving it was accepted for dispatch.
        Assert.ThrowsAsync<HttpRequestException>(
            () => client.FetchAsync(1, "/metrics", CancellationToken.None));
    }
}
