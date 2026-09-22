using System.Text.Json;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.LocalStore.Providers;
using AgentUp.Registry.Shared.Providers;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.LocalStore.Provider;

[TestFixture]
public sealed class LocalRegistryDirectoryStoreTests
{
    [Test]
    public void WritePackage_indexes_and_reads_the_package_back()
    {
        var root = CreateRoot();
        try
        {
            var staged = StageDotnet(root);
            var store = new LocalRegistryDirectoryStore(new RegistryPathValidator(Path.Join(root, "registry")));

            store.WritePackage(staged);
            var package = store.ReadPackage(RegistryDomain.DotnetId, RegistryDomain.DotnetVersion);

            Assert.That(package, Is.Not.Null);
            Assert.That(package!.Manifest.DisplayName, Is.EqualTo(RegistryDomain.DotnetDisplayName));
            Assert.That(store.ReadIndex().Packages.Single().Id, Is.EqualTo(RegistryDomain.DotnetId));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ReadPackage_returns_null_when_the_package_is_missing()
    {
        var root = CreateRoot();
        try
        {
            var store = new LocalRegistryDirectoryStore(new RegistryPathValidator(Path.Join(root, "registry")));

            Assert.That(store.ReadPackage("missing", "1.0.0"), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-registry-" + Guid.NewGuid().ToString("N"))).FullName;

    private static string StageDotnet(string root)
    {
        var staged = Path.Join(root, "staged");
        Directory.CreateDirectory(staged);
        File.WriteAllText(
            Path.Join(staged, "capability.json"),
            JsonSerializer.Serialize(RegistryDomain.DotnetPackage(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        File.WriteAllText(Path.Join(staged, "default.nix"), "{ pkgs }: pkgs.mkShell { packages = [ pkgs.dotnet-sdk_10 ]; }");
        return staged;
    }

    // A package the index does not list is not in the registry, whatever is sitting on disk.
    // This is also what keeps a caller's id and version off the path: they select an entry.
    [Test]
    public void ReadPackage_ignores_a_directory_the_index_never_recorded()
    {
        var root = CreateRoot();
        try
        {
            var registry = Path.Join(root, "registry");
            var store = new LocalRegistryDirectoryStore(new RegistryPathValidator(registry));
            var stray = Path.Join(registry, "packages", RegistryDomain.DotnetId, RegistryDomain.DotnetVersion);
            Directory.CreateDirectory(stray);
            File.WriteAllText(
                Path.Join(stray, "capability.json"),
                JsonSerializer.Serialize(RegistryDomain.DotnetPackage(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));

            Assert.That(store.ReadPackage(RegistryDomain.DotnetId, RegistryDomain.DotnetVersion), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void WritePackage_replaces_the_index_entry_for_the_same_version()
    {
        var root = CreateRoot();
        try
        {
            var staged = StageDotnet(root);
            var store = new LocalRegistryDirectoryStore(new RegistryPathValidator(Path.Join(root, "registry")));

            store.WritePackage(staged);
            store.WritePackage(staged);

            Assert.That(store.ReadIndex().Packages, Has.Count.EqualTo(1));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void WritePackage_keeps_the_index_sorted_so_a_listing_is_comparable()
    {
        var root = CreateRoot();
        try
        {
            var store = new LocalRegistryDirectoryStore(new RegistryPathValidator(Path.Join(root, "registry")));
            store.WritePackage(Stage(root, "second", RegistryDomain.DotnetPackage() with { Id = "docker" }));
            store.WritePackage(Stage(root, "first", RegistryDomain.DotnetPackage()));

            Assert.That(
                store.ReadIndex().Packages.Select(entry => entry.Id),
                Is.EqualTo(new[] { "docker", RegistryDomain.DotnetId }),
                "written second but sorted first");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void WritePackage_rejects_a_directory_with_no_manifest()
    {
        var root = CreateRoot();
        try
        {
            var store = new LocalRegistryDirectoryStore(new RegistryPathValidator(Path.Join(root, "registry")));

            Assert.That(
                () => store.WritePackage(root),
                Throws.InvalidOperationException.With.Message.Contains("capability.json"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ReadPackages_returns_nothing_for_a_registry_with_no_index()
    {
        var root = CreateRoot();
        try
        {
            var store = new LocalRegistryDirectoryStore(new RegistryPathValidator(Path.Join(root, "registry")));

            Assert.That(store.ReadPackages(), Is.Empty);
            Assert.That(store.ReadIndex().Packages, Is.Empty);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ReadStagedPackage_reports_a_staged_directory_with_no_manifest()
    {
        var root = CreateRoot();
        try
        {
            var store = new LocalRegistryDirectoryStore(new RegistryPathValidator(Path.Join(root, "registry")));

            Assert.That(
                () => store.ReadStagedPackage(root),
                Throws.InvalidOperationException.With.Message.Contains("capability.json"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ReadStagedPackage_notices_the_packed_shell_beside_the_manifest()
    {
        var root = CreateRoot();
        try
        {
            var store = new LocalRegistryDirectoryStore(new RegistryPathValidator(Path.Join(root, "registry")));

            Assert.That(store.ReadStagedPackage(StageDotnet(root)).HasDefaultNix, Is.True);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Stage(string root, string name, CapabilityPackageManifest manifest)
    {
        var staged = Path.Join(root, name);
        Directory.CreateDirectory(staged);
        File.WriteAllText(
            Path.Join(staged, "capability.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        return staged;
    }
}
