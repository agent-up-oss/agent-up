using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.Shared.Provider;

[TestFixture]
public sealed class RegistryPathValidatorTests
{
    [Test]
    public void ResolvePackageDirectory_places_a_package_under_the_registry_packages_area()
    {
        var paths = RegistryDomain.Paths(RegistryDomain.RegistryRoot);

        Assert.That(
            paths.ResolvePackageDirectory("dotnet", "1.0.0"),
            Is.EqualTo(Path.Join(paths.RegistryRoot, "packages", "dotnet", "1.0.0")));
    }

    [Test]
    public void StagingRoot_stages_inside_the_registry_root()
    {
        var paths = RegistryDomain.Paths(RegistryDomain.RegistryRoot);

        Assert.That(paths.StagingRoot, Is.EqualTo(Path.Join(paths.RegistryRoot, "staging")));
    }

    // A package directory or staging root reaches the filesystem through the archive provider, so
    // containment is checked again where it is used and not only where it was built.
    [TestCase("..")]
    [TestCase("../escape")]
    [TestCase("")]
    [TestCase("   ")]
    public void RequireWithinRoot_rejects_a_path_outside_the_registry_root(string relative)
    {
        var paths = RegistryDomain.Paths(RegistryDomain.RegistryRoot);

        Assert.That(
            () => paths.RequireWithinRoot(
                relative.Trim().Length == 0 ? relative : Path.Join(paths.RegistryRoot, relative)),
            Throws.InvalidOperationException);
    }

    [Test]
    public void RequireWithinRoot_returns_the_canonical_path_of_a_contained_directory()
    {
        var paths = RegistryDomain.Paths(RegistryDomain.RegistryRoot);

        Assert.That(
            paths.RequireWithinRoot(Path.Join(paths.RegistryRoot, "packages", "dotnet", "..", "dotnet")),
            Is.EqualTo(Path.Join(paths.RegistryRoot, "packages", "dotnet")));
    }

    [Test]
    public void RequireWithinRoot_rejects_the_registry_root_itself()
    {
        var paths = RegistryDomain.Paths(RegistryDomain.RegistryRoot);

        Assert.That(() => paths.RequireWithinRoot(paths.RegistryRoot), Throws.InvalidOperationException);
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
        var paths = RegistryDomain.Paths(RegistryDomain.RegistryRoot);

        Assert.Multiple(() =>
        {
            Assert.That(() => paths.ResolvePackageDirectory(id, "1.0.0"), Throws.InvalidOperationException);
            Assert.That(() => paths.ResolvePackageDirectory("dotnet", id), Throws.InvalidOperationException);
        });
    }
}
