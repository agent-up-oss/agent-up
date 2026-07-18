using System.Diagnostics;
using AgentUp.Server.Features.Mcp.Providers;

namespace AgentUp.Server.Tests.Features.Mcp.TerminalIntegration;

[TestFixture]
public sealed class GitWorkspaceIdentityProviderTests
{
    private const string GitExecutable = "git";

    [Test]
    public async Task ReadAsync_ReturnsGitIdentity_ForRepository()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-git-identity-");
        try
        {
            await RunGitAsync(root.FullName, "init", "-b", "main");
            await RunGitAsync(root.FullName, "config", "user.email", "agent-up@example.test");
            await RunGitAsync(root.FullName, "config", "user.name", "Agent Up");
            await File.WriteAllTextAsync(Path.Join(root.FullName, "README.md"), "# Test\n");
            await RunGitAsync(root.FullName, "add", "README.md");
            await RunGitAsync(root.FullName, "commit", "-m", "Initial commit");

            var provider = new GitWorkspaceIdentityProvider();

            var identity = await provider.ReadAsync(root.FullName, CancellationToken.None);

            Assert.That(identity.RepositoryPath, Is.EqualTo(root.FullName));
            Assert.That(identity.Branch, Is.EqualTo("main"));
            Assert.That(identity.Commit, Does.Match("^[0-9a-f]{40}$"));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task ReadAsync_ReturnsFallbackIdentity_ForNonGitDirectory()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-non-git-identity-");
        try
        {
            var provider = new GitWorkspaceIdentityProvider();

            var identity = await provider.ReadAsync(root.FullName, CancellationToken.None);

            Assert.That(identity.RepositoryPath, Is.EqualTo(root.FullName));
            Assert.That(identity.Branch, Is.EqualTo("not on a git branch"));
            Assert.That(identity.Commit, Is.Empty);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task ReadAsync_ReturnsFallbackIdentity_ForMissingDirectory()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), $"agent-up-missing-git-identity-{Guid.NewGuid():N}");
        var provider = new GitWorkspaceIdentityProvider();

        var identity = await provider.ReadAsync(worktreePath, CancellationToken.None);

        Assert.That(identity.RepositoryPath, Is.EqualTo(worktreePath));
        Assert.That(identity.Branch, Is.EqualTo("not on a git branch"));
        Assert.That(identity.Commit, Is.Empty);
    }

    private static async Task RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GitExecutable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.That(process.ExitCode, Is.Zero, $"git {string.Join(' ', arguments)} failed: {stderr}");
    }
}
