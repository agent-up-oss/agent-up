using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityRegistryRootResolverTests
{
    [Test]
    public void Configured_path_wins_over_a_packed_dev_registry()
    {
        var root = NewTemp();
        try
        {
            WriteIndex(Path.Join(root, "repo", ".agent-up-dev", "capability-registry"));
            var configured = Path.Join(root, "explicit");

            var resolved = CapabilityRegistryRootResolver.Resolve(
                configured,
                Path.Join(root, "data"),
                Path.Join(root, "repo"));

            Assert.That(resolved, Is.EqualTo(Path.GetFullPath(configured)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Walks_up_to_a_packed_agent_up_dev_registry()
    {
        var root = NewTemp();
        try
        {
            var packed = WriteIndex(Path.Join(root, "repo", ".agent-up-dev", "capability-registry"));
            var nested = Path.Join(root, "repo", "AgentUp.Server");
            Directory.CreateDirectory(nested);

            var resolved = CapabilityRegistryRootResolver.Resolve(
                null,
                Path.Join(root, "data"),
                nested);

            Assert.That(resolved, Is.EqualTo(Path.GetFullPath(packed)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Falls_back_to_the_server_data_directory()
    {
        var root = NewTemp();
        try
        {
            var data = Path.Join(root, "data");
            Directory.CreateDirectory(data);

            var resolved = CapabilityRegistryRootResolver.Resolve(null, data, Path.Join(root, "empty"));

            Assert.That(resolved, Is.EqualTo(Path.GetFullPath(Path.Join(data, "capability-registry"))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewTemp()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-registry-root-" + Guid.NewGuid().ToString("N"))).FullName;

    private static string WriteIndex(string registry)
    {
        Directory.CreateDirectory(registry);
        File.WriteAllText(Path.Join(registry, "index.json"), """{"schemaVersion":"1","packages":[]}""");
        return registry;
    }
}
