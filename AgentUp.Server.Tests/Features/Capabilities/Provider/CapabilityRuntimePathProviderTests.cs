using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityRuntimePathProviderTests
{
    [Test]
    public void Directories_are_the_dev_root_bins_that_exist()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-dev-root");
        try
        {
            var bin = Path.Join(root.FullName, "bin");
            var npmBin = Path.Join(root.FullName, "npm", "node_modules", ".bin");
            Directory.CreateDirectory(bin);
            Directory.CreateDirectory(npmBin);

            var provider = new CapabilityRuntimePathProvider(root.FullName);

            Assert.That(provider.Directories(), Is.EqualTo(new[] { bin, npmBin }));
            Assert.That(provider.DevRoot, Is.EqualTo(root.FullName));
        }
        finally { root.Delete(true); }
    }

    [Test]
    public void A_missing_dev_root_adds_nothing()
    {
        var provider = new CapabilityRuntimePathProvider(Path.Join(Path.GetTempPath(), "agent-up-absent-" + Guid.NewGuid().ToString("N")));

        Assert.That(provider.DevRoot, Is.Null);
        Assert.That(provider.Directories(), Is.Empty);
    }

    [Test]
    public void Resolve_finds_the_nearest_dev_root_above_a_search_root()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-dev-walk");
        try
        {
            var devRoot = Directory.CreateDirectory(Path.Join(root.FullName, ".agent-up-dev")).FullName;
            var nested = Directory.CreateDirectory(Path.Join(root.FullName, "AgentUp.Server", "bin")).FullName;

            Assert.That(CapabilityRuntimePathProvider.Resolve(null, nested), Is.EqualTo(devRoot));
            Assert.That(CapabilityRuntimePathProvider.Resolve("/opt/dev", nested), Is.EqualTo(Path.GetFullPath("/opt/dev")));
        }
        finally { root.Delete(true); }
    }
}
