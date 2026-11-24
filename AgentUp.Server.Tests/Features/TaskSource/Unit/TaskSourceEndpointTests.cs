using AgentUp.Server.Features.TaskSource.Models;

namespace AgentUp.Server.Tests.Features.TaskSource.Unit;

[TestFixture]
public sealed class TaskSourceEndpointTests
{
    [Test]
    public void GithubDotComIsKeptAsTheUserFacingOrigin()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TaskSourceEndpoint.TryNormalize(" https://github.com/ ", out var endpoint), Is.True);
            Assert.That(endpoint, Is.EqualTo("https://github.com"));
        });
    }

    [Test]
    public void AForgejoOriginIsKeptWithoutATrailingSlash()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TaskSourceEndpoint.TryNormalize("https://git.example.test/", out var endpoint), Is.True);
            Assert.That(endpoint, Is.EqualTo("https://git.example.test"));
        });
    }

    [Test]
    public void AMissingOrNonHttpValueIsNotAHost()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TaskSourceEndpoint.TryNormalize(" ", out _), Is.False);
            Assert.That(TaskSourceEndpoint.TryNormalize("git.example.test", out _), Is.False);
            Assert.That(TaskSourceEndpoint.TryNormalize("ftp://git.example.test", out _), Is.False);
        });
    }
}
