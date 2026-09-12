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

        Assert.That(entries.Select(item => item.Id), Does.Contain("dotnet").And.Contain("docker"));
    }

    [Test]
    public async Task LoadAllAsync_readsDeclaredCommandAndArguments()
    {
        var path = Path.Join(_directory, "capabilities.json");
        await File.WriteAllTextAsync(path, """
            [
              {
                "id": "codex",
                "versions": [ "dev" ],
                "command": "/opt/codex-acp",
                "arguments": [ "serve" ],
                "versionArguments": [ "--version" ]
              }
            ]
            """);
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, path);

        var entry = (await new CapabilityInventoryFileProvider().LoadAllAsync())
            .Single(item => item.Id.Equals("codex", StringComparison.OrdinalIgnoreCase));

        Assert.Multiple(() =>
        {
            Assert.That(entry.Id, Is.EqualTo("codex"));
            Assert.That(entry.Command, Is.EqualTo("/opt/codex-acp"));
            Assert.That(entry.Arguments, Is.EqualTo(new[] { "serve" }));
            Assert.That(entry.VersionArguments, Is.EqualTo(new[] { "--version" }));
        });
    }

    [Test]
    public void InventoryPathCandidates_includeSystemAndUserFallbacks()
    {
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, null);

        var candidates = CapabilityInventoryFileProvider.InventoryPathCandidates().ToList();

        Assert.That(candidates, Does.Contain("/etc/agent-up/capabilities.json"));
        Assert.That(candidates.Any(path => path.EndsWith(Path.Join(".config", "agent-up", "capabilities.json"), StringComparison.Ordinal)), Is.True);
        Assert.That(candidates.Any(path => path.EndsWith(Path.Join(".config", "agent-up", "capabilities.local.json"), StringComparison.Ordinal)), Is.True);
        var localIndex = candidates.FindIndex(path => path.EndsWith(Path.Join(".config", "agent-up", "capabilities.local.json"), StringComparison.Ordinal));
        var userIndex = candidates.FindIndex(path => path.EndsWith(Path.Join(".config", "agent-up", "capabilities.json"), StringComparison.Ordinal));
        Assert.That(localIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(userIndex, Is.GreaterThan(localIndex));
    }

    [Test]
    public async Task LoadAllAsync_mergesCommandsOntoVersionOnlyInstallmentInventory()
    {
        var previousHome = Environment.GetEnvironmentVariable("HOME");
        var previousCwd = Directory.GetCurrentDirectory();
        try
        {
            Environment.SetEnvironmentVariable("HOME", _directory);
            Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, null);
            Directory.SetCurrentDirectory(_directory);

            var installment = Path.Join(_directory, ".config", "agent-up", "capabilities.json");
            Directory.CreateDirectory(Path.GetDirectoryName(installment)!);
            await File.WriteAllTextAsync(installment, """[{ "id": "dotnet", "versions": [ "10.0.x" ] }]""");
            await File.WriteAllTextAsync(
                Path.Join(_directory, ".config", "agent-up", "capabilities.local.json"),
                """[{ "id": "codex", "versions": [ "dev" ], "command": "/opt/codex-acp", "arguments": [] }]""");

            var entries = await new CapabilityInventoryFileProvider().LoadAllAsync();

            Assert.Multiple(() =>
            {
                Assert.That(entries.Single(item => item.Id == "dotnet").Versions, Is.EqualTo(new[] { "10.0.x" }));
                Assert.That(entries.Single(item => item.Id == "codex").Command, Is.EqualTo("/opt/codex-acp"));
            });
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
            Environment.SetEnvironmentVariable("HOME", previousHome);
        }
    }

    [Test]
    public async Task LoadAllAsync_fillsCommandFromLowerPriorityFileForTheSameCapability()
    {
        var versionsOnly = Path.Join(_directory, "installment.json");
        await File.WriteAllTextAsync(versionsOnly, """[{ "id": "codex", "versions": [ "dev" ] }]""");
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, versionsOnly);

        var previousHome = Environment.GetEnvironmentVariable("HOME");
        var previousCwd = Directory.GetCurrentDirectory();
        try
        {
            Environment.SetEnvironmentVariable("HOME", _directory);
            Directory.SetCurrentDirectory(_directory);
            var overlay = Path.Join(_directory, ".config", "agent-up", "capabilities.local.json");
            Directory.CreateDirectory(Path.GetDirectoryName(overlay)!);
            await File.WriteAllTextAsync(overlay, """[{ "id": "codex", "command": "/opt/from-disk/codex-acp" }]""");

            var entry = (await new CapabilityInventoryFileProvider().LoadAllAsync())
                .Single(item => item.Id.Equals("codex", StringComparison.OrdinalIgnoreCase));

            Assert.Multiple(() =>
            {
                Assert.That(entry.Versions, Is.EqualTo(new[] { "dev" }));
                Assert.That(entry.Command, Is.EqualTo("/opt/from-disk/codex-acp"));
            });
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
            Environment.SetEnvironmentVariable("HOME", previousHome);
        }
    }

    [Test]
    public async Task LoadAllAsync_letsTheUserOverlayWinOverTheUserInstallmentFile()
    {
        var previousHome = Environment.GetEnvironmentVariable("HOME");
        var previousCwd = Directory.GetCurrentDirectory();
        try
        {
            Environment.SetEnvironmentVariable("HOME", _directory);
            Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, null);
            Directory.SetCurrentDirectory(_directory);
            var config = Path.Join(_directory, ".config", "agent-up");
            Directory.CreateDirectory(config);
            await File.WriteAllTextAsync(
                Path.Join(config, "capabilities.json"),
                """[{ "id": "codex", "versions": [ "installment" ], "command": "/opt/installment/codex-acp" }]""");
            await File.WriteAllTextAsync(
                Path.Join(config, "capabilities.local.json"),
                """[{ "id": "codex", "command": "/opt/local/codex-acp" }]""");

            var entry = (await new CapabilityInventoryFileProvider().LoadAllAsync())
                .Single(item => item.Id.Equals("codex", StringComparison.OrdinalIgnoreCase));

            Assert.Multiple(() =>
            {
                Assert.That(entry.Command, Is.EqualTo("/opt/local/codex-acp"));
                Assert.That(entry.Versions, Is.EqualTo(new[] { "installment" }));
            });
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
            Environment.SetEnvironmentVariable("HOME", previousHome);
        }
    }

    [Test]
    public async Task LoadAllAsync_readsRepoDevInventoryWhenWalkingFromCurrentDirectory()
    {
        var previousHome = Environment.GetEnvironmentVariable("HOME");
        var previousCwd = Directory.GetCurrentDirectory();
        try
        {
            Environment.SetEnvironmentVariable("HOME", Path.Join(_directory, "empty-home"));
            Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, null);
            var project = Path.Join(_directory, "repo", "AgentUp.Server");
            Directory.CreateDirectory(project);
            Directory.CreateDirectory(Path.Join(_directory, "repo", ".agent-up-dev"));
            await File.WriteAllTextAsync(
                Path.Join(_directory, "repo", ".agent-up-dev", "capabilities.json"),
                """[{ "id": "cursor", "versions": [ "dev" ], "command": "/opt/agent", "arguments": [ "acp" ] }]""");
            Directory.SetCurrentDirectory(project);

            var entry = (await new CapabilityInventoryFileProvider().LoadAllAsync())
                .Single(item => item.Id.Equals("cursor", StringComparison.OrdinalIgnoreCase));

            Assert.That(entry.Command, Is.EqualTo("/opt/agent"));
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
            Environment.SetEnvironmentVariable("HOME", previousHome);
        }
    }

    [Test]
    public async Task LoadAllAsync_returnsEmptyWhenNoInventoryFilesExist()
    {
        var previousHome = Environment.GetEnvironmentVariable("HOME");
        var previousCwd = Directory.GetCurrentDirectory();
        try
        {
            Environment.SetEnvironmentVariable("HOME", Path.Join(_directory, "empty-home"));
            Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, null);
            var isolated = Path.Join(_directory, "isolated");
            Directory.CreateDirectory(isolated);
            Directory.SetCurrentDirectory(isolated);

            Assert.That(await new CapabilityInventoryFileProvider().LoadAllAsync(), Is.Empty);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
            Environment.SetEnvironmentVariable("HOME", previousHome);
        }
    }
}
