using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Host.Provider;

[TestFixture]
public sealed class DebugPathValidatorTests
{
    [Test]
    public void JoinUnderRoot_rejectsEscape()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-paths", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var validator = new DebugPathValidator(root);

        Assert.That(() => validator.JoinUnderRoot("..", "etc"), Throws.InvalidOperationException);
        Assert.That(validator.SessionDirectory, Does.StartWith(Path.GetFullPath(root)));
    }

    [Test]
    public void BashQuote_escapesSingleQuotes()
    {
        Assert.That(BashQuote.Single("it's"), Is.EqualTo("'it'\\''s'"));
    }

    [Test]
    public void RepositoryRoot_findsSolution()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-root", Guid.NewGuid().ToString("N"));
        var nested = Path.Join(root, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Join(root, "agent-up.sln"), "");

        Assert.That(RepositoryRootProvider.Find(nested), Is.EqualTo(Path.GetFullPath(root)));
    }
}
