using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class GitChangeOutputParserTests
{
    [Test]
    public void ParseStatus_keepsTheFirstCharacterOfAModifiedPath()
    {
        // Regression: trimming the entry first ate the porcelain status column's leading
        // space, so index 3 landed one character into the path and every modified file
        // became unmatched — reported as an incomplete map instead of a required check.
        var parsed = new GitChangeOutputParser().ParseStatus(" M AgentUp.Server/Program.cs\0");

        Assert.That(parsed, Is.EqualTo(new[] { "AgentUp.Server/Program.cs" }));
    }

    [Test]
    public void ParseStatus_readsStagedUntrackedAndDeletedEntriesAlike()
    {
        var output = string.Concat(
            "M  AgentUp.CLI/Program.cs\0",
            "?? AgentUp.Verification/New.cs\0",
            " D packaging/old.service\0",
            "MM AgentUp.Server/Both.cs\0");

        Assert.That(new GitChangeOutputParser().ParseStatus(output), Is.EqualTo(new[]
        {
            "AgentUp.CLI/Program.cs",
            "AgentUp.Verification/New.cs",
            "packaging/old.service",
            "AgentUp.Server/Both.cs"
        }));
    }

    [Test]
    public void ParseStatus_handlesAPathContainingASpace()
    {
        var parsed = new GitChangeOutputParser().ParseStatus(" M docs/a guide.md\0");

        Assert.That(parsed, Is.EqualTo(new[] { "docs/a guide.md" }));
    }

    [Test]
    public void ParseStatus_returnsNothingForEmptyOutput()
    {
        Assert.That(new GitChangeOutputParser().ParseStatus(string.Empty), Is.Empty);
    }

    [Test]
    public void ParseNames_readsBarePathsFromADiff()
    {
        var parsed = new GitChangeOutputParser().ParseNames("AgentUp.Server/Program.cs\0docs/index.md\0");

        Assert.That(parsed, Is.EqualTo(new[] { "AgentUp.Server/Program.cs", "docs/index.md" }));
    }

    [Test]
    public void ParseNames_returnsNothingForEmptyOutput()
    {
        Assert.That(new GitChangeOutputParser().ParseNames(string.Empty), Is.Empty);
    }

    [Test]
    public void ParseStatus_ignoresACollapsedUntrackedDirectoryEntry()
    {
        // Git reports a wholly untracked directory as one trailing-slash entry unless
        // --untracked-files=all is passed. A directory cannot be hashed, so accepting one
        // would record it as "absent" and hide every new file inside it.
        var parsed = new GitChangeOutputParser().ParseStatus("?? AgentUp.Verification/\0?? AgentUp.Verification/New.cs\0");

        Assert.That(parsed, Is.EqualTo(new[] { "AgentUp.Verification/New.cs" }));
    }

    [Test]
    public void ParseNames_ignoresDirectoryEntries()
    {
        var parsed = new GitChangeOutputParser().ParseNames("packaging/\0packaging/agent-up.service\0");

        Assert.That(parsed, Is.EqualTo(new[] { "packaging/agent-up.service" }));
    }
}
