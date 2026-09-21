using AgentUp.Registry.Shared.Providers;

namespace AgentUp.Registry.Tests.Features.Shared.Provider;

[TestFixture]
public sealed class RegistryPathValidatorTests
{
    [Test]
    public void ResolvePackageDirectory_places_a_package_under_the_registry_packages_area()
    {
        var paths = new RegistryPathValidator(Path.Join(Path.GetTempPath(), "agent-up-registry"));

        Assert.That(
            paths.ResolvePackageDirectory("dotnet", "1.0.0"),
            Is.EqualTo(Path.Join(paths.RegistryRoot, "packages", "dotnet", "1.0.0")));
    }

    [Test]
    public void ResolveStagingDirectory_stages_inside_the_registry_root()
    {
        var paths = new RegistryPathValidator(Path.Join(Path.GetTempPath(), "agent-up-registry"));

        Assert.That(
            paths.ResolveStagingDirectory("dotnet", "1.0.0"),
            Is.EqualTo(Path.Join(paths.RegistryRoot, "staging", "dotnet", "1.0.0")));
    }

    // Ids and versions reach the validator straight off an HTTP route, so a traversal attempt has
    // to be refused rather than folded into some other package's directory.
    [TestCase("..")]
    [TestCase(".")]
    [TestCase("../escape")]
    [TestCase("nested/id")]
    [TestCase("nested\\id")]
    [TestCase("c:drive")]
    [TestCase("")]
    [TestCase("   ")]
    public void Resolve_rejects_an_id_that_is_not_a_single_segment(string id)
    {
        var paths = new RegistryPathValidator(Path.Join(Path.GetTempPath(), "agent-up-registry"));

        Assert.Multiple(() =>
        {
            Assert.That(() => paths.ResolvePackageDirectory(id, "1.0.0"), Throws.InvalidOperationException);
            Assert.That(() => paths.ResolveStagingDirectory(id, "1.0.0"), Throws.InvalidOperationException);
            Assert.That(() => paths.ResolvePackageDirectory("dotnet", id), Throws.InvalidOperationException);
        });
    }
}
