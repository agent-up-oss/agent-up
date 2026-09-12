using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class ContentHashProviderTests
{
    [Test]
    public void HashText_isStableForIdenticalContent()
    {
        var hashes = new ContentHashProvider();

        Assert.That(hashes.HashText("public sealed class Thing;"),
            Is.EqualTo(hashes.HashText("public sealed class Thing;")));
    }

    [Test]
    public void HashText_differsForAOneCharacterEdit()
    {
        var hashes = new ContentHashProvider();

        Assert.That(hashes.HashText("var total = 1;"), Is.Not.EqualTo(hashes.HashText("var total = 2;")));
    }

    [Test]
    public void HashFile_reportsAbsentForAMissingPathSoDeletionCountsAsAChange()
    {
        var hashes = new ContentHashProvider();
        var missing = Path.Join(TestContext.CurrentContext.WorkDirectory, "does-not-exist-" + Guid.NewGuid().ToString("N"));

        Assert.That(hashes.HashFile(missing), Is.EqualTo(ContentHashProvider.DeletedMarker));
    }

    [Test]
    public void HashFile_matchesHashTextForTheSameBytes()
    {
        var hashes = new ContentHashProvider();
        var directoryPath = Path.Join(TestContext.CurrentContext.WorkDirectory, "hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Join(directoryPath, "sample.cs");
        File.WriteAllText(filePath, "namespace Sample;");

        Assert.That(hashes.HashFile(filePath), Is.EqualTo(hashes.HashText("namespace Sample;")));

        Directory.Delete(directoryPath, recursive: true);
    }
}
