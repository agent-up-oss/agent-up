using AgentUp.Packaging.Shared.Providers;

namespace AgentUp.Packaging.Tests.Features.ReleaseArtifacts.Provider;

[TestFixture]
public class PackagePathValidatorTests
{
    [Test]
    public void ResolveRelativeUnderRoot_rejectsTraversal()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-PackagePathValidatorTests");

        var exception = Assert.Throws<ArgumentException>(() =>
            PackagePathValidator.ResolveRelativeUnderRoot(root, Path.Join("artifacts", "..", "..", "outside"), "path"));

        Assert.That(exception!.ParamName, Is.EqualTo("path"));
    }

    [Test]
    public void ResolveRelativeUnderRoot_returnsFullPathUnderRoot()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-PackagePathValidatorTests");

        var path = PackagePathValidator.ResolveRelativeUnderRoot(root, Path.Join("artifacts", "release"), "path");

        Assert.That(path, Is.EqualTo(Path.GetFullPath(Path.Join(root, "artifacts", "release"))));
    }

    [Test]
    public void RequireSafePathComponent_rejectsSeparators()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            PackagePathValidator.RequireSafePathComponent("ubuntu/../../x", "component"));

        Assert.That(exception!.ParamName, Is.EqualTo("component"));
    }

    [Test]
    public void PackageFileSystem_rejectsRelativePaths()
    {
        var files = new TestPackageFileSystem();

        var exception = Assert.Throws<ArgumentException>(() => files.CreateDirectory(Path.Join("relative", "path")));

        Assert.That(exception!.ParamName, Is.EqualTo("path"));
    }

    private sealed class TestPackageFileSystem : PackageFileSystem;
}
