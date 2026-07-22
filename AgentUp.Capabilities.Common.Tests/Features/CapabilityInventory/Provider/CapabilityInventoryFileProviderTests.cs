using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityInventory.Provider;

[TestFixture]
public sealed class CapabilityInventoryFileProviderTests
{
    private string? _previousInventoryPath;
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _previousInventoryPath = Environment.GetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable);
        _directory = Path.Join(Path.GetTempPath(), "AgentUp-CapabilityInventory", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, _previousInventoryPath);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public async Task LoadAsync_readsDeclaredVersionsForRequestedCapability()
    {
        var path = Path.Join(_directory, "capabilities.json");
        await File.WriteAllTextAsync(path, """
            [
              { "id": "dotnet", "versions": [ "10.0.x", "9.0.x" ] },
              { "id": "docker", "versions": [ "27.x" ] }
            ]
            """);
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, path);

        var versions = await new CapabilityInventoryFileProvider().LoadAsync("dotnet");

        Assert.That(versions.Select(item => item.Version), Is.EqualTo(new[] { "10.0.x", "9.0.x" }));
        Assert.That(versions.All(item => item.IsManaged), Is.True);
    }

    [Test]
    public async Task LoadAllAsync_readsEveryDeclaredCapability()
    {
        var path = Path.Join(_directory, "capabilities.json");
        await File.WriteAllTextAsync(path, """
            [
              { "id": "dotnet", "versions": [ "10.0.x" ] },
              { "id": "docker", "versions": [ "27.x" ] }
            ]
            """);
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, path);

        var entries = await new CapabilityInventoryFileProvider().LoadAllAsync();

        Assert.That(entries.Select(item => item.Id), Is.EqualTo(new[] { "dotnet", "docker" }));
    }

    [Test]
    public void InventoryPathCandidates_includeSystemAndUserFallbacks()
    {
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, null);

        var candidates = CapabilityInventoryFileProvider.InventoryPathCandidates();

        Assert.That(candidates, Does.Contain("/etc/agent-up/capabilities.json"));
        Assert.That(candidates.Any(path => path.EndsWith(Path.Join(".config", "agent-up", "capabilities.json"), StringComparison.Ordinal)), Is.True);
    }
}
