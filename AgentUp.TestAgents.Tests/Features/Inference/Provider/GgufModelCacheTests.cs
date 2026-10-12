using System.Security.Cryptography;
using AgentUp.TestAgents.Features.Inference.Models;
using AgentUp.TestAgents.Features.Inference.Providers;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Inference.Provider;

[TestFixture]
public sealed class GgufModelCacheTests
{
    [Test]
    public async Task EnsureAsync_downloadsOnACacheMissAndReturnsThePinnedFile()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-gguf").FullName;
        try
        {
            var bytes = "gguf"u8.ToArray();
            var spec = Spec(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            var downloader = new FakeHttpDownloader { Bytes = bytes };
            var path = await new GgufModelCache(downloader, root).EnsureAsync(spec, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(downloader.Calls, Is.EqualTo(1));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task EnsureAsync_deletesACachedFileWhoseHashDoesNotMatch()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-gguf").FullName;
        try
        {
            var spec = Spec(Convert.ToHexString(SHA256.HashData("expected"u8.ToArray())).ToLowerInvariant());
            var cached = Path.Join(root, spec.FileName);
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(cached, "wrong");

            var exception = Assert.ThrowsAsync<InvalidOperationException>(
                () => new GgufModelCache(new FakeHttpDownloader(), root).EnsureAsync(spec, CancellationToken.None));

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Does.Contain("did not match SHA-256"));
                Assert.That(File.Exists(cached), Is.False);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static GgufModelSpec Spec(string sha) => new(
        "pin",
        "model.gguf",
        sha,
        "https://example.test/model.gguf",
        512);
}
