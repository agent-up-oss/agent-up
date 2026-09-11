using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class PathGlobProviderTests
{
    [TestCase("AgentUp.Server/**", "AgentUp.Server/Features/Git/Services/GitChangesService.cs")]
    [TestCase("AgentUp.Server/**", "AgentUp.Server/Program.cs")]
    [TestCase("**/*.md", "README.md")]
    [TestCase("**/*.md", "docs/developer-guide/testing.md")]
    [TestCase("packaging/**", "packaging/linux/agent-up.service")]
    [TestCase("*.sln", "agent-up.sln")]
    [TestCase("AgentUp.Mobile/src/**", "AgentUp.Mobile/src/features/git/providers/GitApiProvider.test.ts")]
    public void Matches_acceptsPathsTheRuleIsMeantToCover(string glob, string path)
    {
        var globs = new PathGlobProvider();

        Assert.That(globs.Matches(glob, path), Is.True);
    }

    [Test]
    public void Matches_doesNotLetAProjectGlobLeakIntoItsTestProject()
    {
        var globs = new PathGlobProvider();

        Assert.That(globs.Matches("AgentUp.Server/**", "AgentUp.Server.Tests/Features/Git/Unit/GitChangesServiceTests.cs"), Is.False);
    }

    [Test]
    public void Matches_treatsSingleStarAsSegmentScoped()
    {
        var globs = new PathGlobProvider();

        Assert.Multiple(() =>
        {
            Assert.That(globs.Matches("docs/*.md", "docs/index.md"), Is.True);
            Assert.That(globs.Matches("docs/*.md", "docs/developer-guide/index.md"), Is.False);
        });
    }

    [Test]
    public void Matches_normalizesWindowsSeparators()
    {
        var globs = new PathGlobProvider();

        Assert.That(globs.Matches("AgentUp.Server/**", "AgentUp.Server\\Program.cs"), Is.True);
    }

    [Test]
    public void Matches_escapesRegexMetacharactersInLiteralSegments()
    {
        var globs = new PathGlobProvider();

        // The dot in the project name must not behave as a regex wildcard.
        Assert.That(globs.Matches("AgentUp.Server/**", "AgentUpXServer/Program.cs"), Is.False);
    }
}
