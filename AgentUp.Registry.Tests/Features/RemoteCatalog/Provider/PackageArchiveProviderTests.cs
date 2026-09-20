using AgentUp.Registry.Features.RemoteCatalog.Providers;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.RemoteCatalog.Provider;

[TestFixture]
public sealed class PackageArchiveProviderTests
{
    [Test]
    public void ZipDirectory_round_trips_capability_json()
    {
        var root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-zip-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var staged = Path.Join(root, "pkg");
            Directory.CreateDirectory(staged);
            File.WriteAllText(Path.Join(staged, "capability.json"), "{\"id\":\"dotnet\"}");
            var provider = new PackageArchiveProvider();

            var unzipped = provider.Unzip(provider.ZipDirectory(staged), Path.Join(root, "out"), RegistryDomain.DotnetId, RegistryDomain.DotnetVersion);

            Assert.That(File.ReadAllText(Path.Join(unzipped, "capability.json")), Does.Contain("dotnet"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Unzip_rejects_path_separators_in_ids()
    {
        var provider = new PackageArchiveProvider();

        Assert.That(
            () => provider.Unzip([0x50, 0x4B, 0x05, 0x06], "/tmp", "../escape", "1.0.0"),
            Throws.InvalidOperationException);
    }
}
