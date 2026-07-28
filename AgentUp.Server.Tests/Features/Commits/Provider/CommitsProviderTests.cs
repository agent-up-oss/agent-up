using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Providers;

namespace AgentUp.Server.Tests.Features.Commits.Provider;

[TestFixture]
public sealed class CommitsProviderTests
{
    [Test]
    public void CommitsGitProvider_implementsInterface()
    {
        var provider = new CommitsGitProvider();

        Assert.That(provider, Is.InstanceOf<ICommitsGitProvider>());
    }

    [Test]
    public void CommitsQueueProvider_implementsInterface()
    {
        var provider = new CommitsQueueProvider(new CommitsGitProvider());

        Assert.That(provider, Is.InstanceOf<ICommitsQueueProvider>());
    }
}
