using System.Text;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Forge.Provider;

[TestFixture]
public sealed class ForgeGitTransportProviderTests
{
    [Test]
    public void CredentialEnvironment_sendsTheTokenAsAHeaderOnlyToAnHttpRemote()
    {
        var https = ForgeGitTransportProvider.CredentialEnvironment("https://forge.example/acme/widgets.git", "secret");
        var ssh = ForgeGitTransportProvider.CredentialEnvironment("git@forge.example:acme/widgets.git", "secret");
        var expected = "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:secret"));

        Assert.Multiple(() =>
        {
            Assert.That(https["GIT_CONFIG_KEY_0"], Is.EqualTo("http.extraHeader"));
            Assert.That(https["GIT_CONFIG_VALUE_0"], Is.EqualTo(expected));
            Assert.That(ssh, Is.Empty);
        });
    }

    [Test]
    public async Task PushAsync_andFetchAsync_moveTheTaskBranchThroughTheRemote()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"forge-transport-{Guid.NewGuid():N}");
        var author = Path.Join(root, "author");
        var reviewer = Path.Join(root, "reviewer");
        var remote = Path.Join(root, "remote.git");
        await TestGitRepository.InitializeAsync(author);
        await File.WriteAllTextAsync(Path.Join(author, "greeting.txt"), "Ahoy\n");
        await TestGitRepository.CommitAllAsync(author, "initial");
        await TestGitRepository.RunAsync(root, "init", "--bare", "-q", remote);
        await TestGitRepository.InitializeAsync(reviewer);
        var transport = new ForgeGitTransportProvider();

        await transport.PushAsync(author, remote, "agent-up/issue-1", "token", CancellationToken.None);
        await transport.FetchAsync(reviewer, remote, ["agent-up/issue-1"], "token", CancellationToken.None);

        Assert.That(
            await TestGitRepository.ReadAsync(reviewer, "rev-parse", "refs/remotes/origin/agent-up/issue-1"),
            Is.EqualTo(await TestGitRepository.ReadAsync(author, "rev-parse", "HEAD")));
        Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task PushAsync_reportsARefusalWithoutTheToken()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"forge-transport-{Guid.NewGuid():N}");
        var author = Path.Join(root, "author");
        await TestGitRepository.InitializeAsync(author);
        await File.WriteAllTextAsync(Path.Join(author, "a.txt"), "a\n");
        await TestGitRepository.CommitAllAsync(author, "initial");

        var refused = Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ForgeGitTransportProvider().PushAsync(author, Path.Join(root, "missing.git"), "agent-up/issue-1", "missing.git-secret", CancellationToken.None));

        Assert.That(refused!.Message, Does.StartWith("git push failed").And.Not.Contain("missing.git-secret"));
        Directory.Delete(root, recursive: true);
    }
}
