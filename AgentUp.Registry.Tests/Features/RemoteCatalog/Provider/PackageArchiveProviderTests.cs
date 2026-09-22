using AgentUp.Registry.Features.RemoteCatalog.Providers;
using AgentUp.Registry.Shared.Providers;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.RemoteCatalog.Provider;

[TestFixture]
public sealed class PackageArchiveProviderTests
{
    [Test]
    public void ZipDirectory_round_trips_capability_json()
    {
        var root = CreateRegistryRoot();
        try
        {
            var provider = new PackageArchiveProvider(RegistryDomain.Paths(root));
            var staged = Path.Join(root, "staging", "pkg");
            Directory.CreateDirectory(staged);
            File.WriteAllText(Path.Join(staged, "capability.json"), "{\"id\":\"dotnet\"}");

            var unzipped = provider.Unzip(
                provider.ZipDirectory(staged),
                Path.Join(root, "staging"),
                RegistryDomain.DotnetId,
                RegistryDomain.DotnetVersion);

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
        var root = CreateRegistryRoot();
        try
        {
            var provider = new PackageArchiveProvider(RegistryDomain.Paths(root));

            Assert.That(
                () => provider.Unzip([0x50, 0x4B, 0x05, 0x06], Path.Join(root, "staging"), "../escape", "1.0.0"),
                Throws.InvalidOperationException);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Unzip_rejects_a_destination_outside_the_registry_root()
    {
        var root = CreateRegistryRoot();
        try
        {
            var provider = new PackageArchiveProvider(RegistryDomain.Paths(root));

            Assert.That(
                () => provider.Unzip([0x50, 0x4B, 0x05, 0x06], Path.Join(root, "..", "elsewhere"), "dotnet", "1.0.0"),
                Throws.InvalidOperationException.With.Message.Contains("escaped the registry root"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ZipDirectory_rejects_a_package_directory_outside_the_registry_root()
    {
        var root = CreateRegistryRoot();
        try
        {
            var provider = new PackageArchiveProvider(RegistryDomain.Paths(Path.Join(root, "registry")));

            Assert.That(
                () => provider.ZipDirectory(root),
                Throws.InvalidOperationException.With.Message.Contains("escaped the registry root"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRegistryRoot()
        => Directory.CreateDirectory(
            Path.Join(Path.GetTempPath(), "agent-up-zip-" + Guid.NewGuid().ToString("N"))).FullName;
}
