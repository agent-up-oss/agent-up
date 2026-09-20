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
}
