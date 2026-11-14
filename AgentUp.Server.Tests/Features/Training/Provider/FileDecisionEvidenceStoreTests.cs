using AgentUp.Server.Features.Training.Providers;

namespace AgentUp.Server.Tests.Features.Training.Provider;

[TestFixture]
public sealed class FileDecisionEvidenceStoreTests
{
    [Test]
    public async Task LoadAsync_returnsWhatSaveAsyncStored()
    {
        using var data = new TemporaryDirectory();
        var store = new FileDecisionEvidenceStore(data.Path);

        var saved = await store.SaveAsync("artifact-1", "image/png", [1, 2, 3], CancellationToken.None);
        var loaded = await store.LoadAsync(saved.EvidenceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(loaded?.Bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(loaded?.Image.SourceArtifactId, Is.EqualTo("artifact-1"));
        });
    }

    [TestCase("../outside")]
    [TestCase("..")]
    [TestCase("/etc")]
    [TestCase("0123456789abcdef0123456789abcde/")]
    [TestCase("not-an-evidence-id")]
    public async Task LoadAsync_refusesAnIdThatIsNotOneTheStoreMinted(string evidenceId)
    {
        using var data = new TemporaryDirectory();
        var store = new FileDecisionEvidenceStore(data.Path);
        // A descriptor planted beside the evidence root, where a traversing id would land.
        var outside = System.IO.Path.Join(data.Path, "training", "outside");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(System.IO.Path.Join(outside, "evidence.json"), """{"EvidenceId":"x","SourceArtifactId":"y","MimeType":"image/png"}""");
        await File.WriteAllBytesAsync(System.IO.Path.Join(outside, "evidence.png"), [9]);

        Assert.That(await store.LoadAsync(evidenceId, CancellationToken.None), Is.Null);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), "agentup-evidence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
