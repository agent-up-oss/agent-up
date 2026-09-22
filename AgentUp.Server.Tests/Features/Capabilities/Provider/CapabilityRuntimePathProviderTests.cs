using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityRuntimePathProviderTests
{
    [Test]
    public void Directories_includes_package_bin_and_registry_sibling_bins()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-runtime-path");
        try
        {
            var registry = Path.Join(root.FullName, "capability-registry");
            var packageBin = Path.Join(root.FullName, "packages", "codex", "1.0.0", "bin");
            var npmBin = Path.Join(root.FullName, "npm", "node_modules", ".bin");
            Directory.CreateDirectory(packageBin);
            Directory.CreateDirectory(npmBin);

            var directories = new CapabilityRuntimePathProvider()
                .Directories(registry, [Path.GetDirectoryName(packageBin)!]);

            Assert.That(directories, Does.Contain(packageBin));
            Assert.That(directories, Does.Contain(npmBin));
        }
        finally { root.Delete(true); }
    }

    [Test]
    public void DevRoot_is_the_registry_parent_when_dev_bins_exist()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-runtime-root");
        try
        {
            var registry = Path.Join(root.FullName, "capability-registry");
            Directory.CreateDirectory(Path.Join(root.FullName, "bin"));

            Assert.That(new CapabilityRuntimePathProvider().DevRoot(registry), Is.EqualTo(root.FullName));
        }
        finally { root.Delete(true); }
    }
}
