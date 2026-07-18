using AgentUp.InstallerApp.Features.Installation.ViewModels;
using AgentUp.InstallerApp.Features.Installation.Views;
using AgentUp.InstallerApp.Tests.Support;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Services;
using AgentUp.InstallerApp.Features.Capabilities.Providers;
using AgentUp.InstallerApp.Features.Capabilities.Services;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.NixOsInstallation.Interfaces;
using AgentUp.Installers.Features.NixOsInstallation.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;

namespace AgentUp.InstallerApp.Tests.Features.Installation.Headless;

[TestFixture]
public class InstallerWindowBehaviorTests
{
    [AvaloniaTest]
    public async Task Window_startsOnDashboardWithCoreCardsAndAddModuleCard()
    {
        var window = await LaunchAsync();

        Assert.That(window.Find<TextBlock>("PageTitle").Text, Is.EqualTo("Dashboard"));
        Assert.That(window.Find<ItemsControl>("ComponentCards").ItemCount, Is.EqualTo(3));
        Assert.That(window.Find<Button>("AddModuleCard").IsVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task CoreCard_install_updatesCardToInstalled()
    {
        var window = await LaunchAsync();
        var model = (InstallerViewModel)window.DataContext!;
        var desktop = model.ComponentCards.Single(card => card.Title == "Desktop");

        desktop.InstallCommand.Execute(null);
        await HeadlessExtensions.FlushAsync();
        await HeadlessExtensions.FlushAsync();

        Assert.That(desktop.StatusText, Is.EqualTo("Installed"));
        Assert.That(desktop.Progress, Is.EqualTo(100));
    }

    [AvaloniaTest]
    public async Task AddModule_installsCatalogCapabilityAndShowsEditPanel()
    {
        var window = await LaunchAsync();
        var model = (InstallerViewModel)window.DataContext!;

        window.Find<Button>("AddModuleCard").Command!.Execute(null);
        await HeadlessExtensions.FlushAsync();

        Assert.That(window.Find<ItemsControl>("CatalogEntries").ItemCount, Is.GreaterThanOrEqualTo(2));

        var dotnet = model.CatalogEntries.Single(entry => entry.Entry.Id == "dotnet");
        dotnet.InstallCommand.Execute(null);
        await HeadlessExtensions.FlushAsync();
        await HeadlessExtensions.FlushAsync();

        var card = model.CapabilityCards.Single(item => item.Id == "dotnet");
        Assert.That(card.StatusText, Is.EqualTo("Installed"));

        card.EditCommand.Execute(null);
        await HeadlessExtensions.FlushAsync();

        Assert.That(window.Find<Border>("CapabilityEditPanel").IsVisible, Is.True);
        Assert.That(window.Find<ItemsControl>("CapabilityVersions").ItemCount, Is.EqualTo(1));
    }

    [AvaloniaTest]
    public async Task NixOsDashboard_loadsDeclaredCapabilitiesAndDisablesInstallActions()
    {
        var previousInventoryPath = Environment.GetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable);
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstallerApp-NixOs", Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        var inventory = Path.Join(root, "capabilities.json");
        await File.WriteAllTextAsync(inventory, """
            [
              { "id": "dotnet", "versions": [ "10.0.x", "9.0.x" ] }
            ]
            """);
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, inventory);

        try
        {
            var version = new Version(1, 2, 3);
            var model = new InstallerViewModel(
                InstallerSession.CreateDefault("Agent-Up", version, "/opt/agent-up", PayloadSelection.Bundled(version)),
                new NixOsInstallerPlatformAdapter(
                    new Lookup(("agent-up", "/nix/store/agent-up/bin/agent-up")),
                    new DockerPrerequisite(new DockerProvider(), new Version(27, 0, 0))),
                new CapabilityDashboardService(
                    new OfficialCapabilityCatalogProvider(),
                    new NixOsCapabilityModuleStore(new CapabilityInventoryFileProvider()),
                    new CapabilityInstallPlanner(new CapabilityToolCacheLayout(Path.Join(root, "tool-cache"))),
                    false));
            var window = new InstallerWindow { DataContext = model };
            window.Show();
            await model.RefreshAsync();
            await HeadlessExtensions.FlushAsync();

            var cli = model.ComponentCards.Single(card => card.Target == InstallerComponentTarget.Cli);
            var dotnet = model.CapabilityCards.Single(card => card.Id == "dotnet");

            Assert.That(cli.StatusText, Is.EqualTo("Installed"));
            Assert.That(cli.InstallCommand.CanExecute(null), Is.False);
            Assert.That(cli.PrimaryButtonText, Is.EqualTo("Managed by NixOS"));
            Assert.That(dotnet.StatusText, Is.EqualTo("Installed"));
            Assert.That(dotnet.Versions, Has.Count.EqualTo(2));

            window.Find<Button>("AddModuleCard").Command!.Execute(null);
            await HeadlessExtensions.FlushAsync();

            var docker = model.CatalogEntries.Single(entry => entry.Entry.Id == "docker");
            Assert.That(docker.InstallCommand.CanExecute(null), Is.False);
            Assert.That(docker.ButtonText, Is.EqualTo("Managed by NixOS"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, previousInventoryPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ComponentCards_useConstrainedActionGridAndHideMutationButtonsInLookupOnlyMode()
    {
        var xaml = File.ReadAllText(Path.Join(
            FindRepositoryRoot(TestContext.CurrentContext.TestDirectory),
            "AgentUp.InstallerApp",
            "Features",
            "Installation",
            "Views",
            "InstallerWindow.axaml"));

        Assert.That(xaml, Does.Contain("ColumnDefinitions=\"*,Auto,Auto\""));
        Assert.That(xaml, Does.Contain("TextTrimming=\"CharacterEllipsis\""));
        Assert.That(xaml, Does.Contain("IsVisible=\"{Binding SupportsInstallActions}\""));
    }

    private static async Task<InstallerWindow> LaunchAsync()
    {
        var window = new InstallerWindow { DataContext = InstallerViewModel.CreateFakeForTests() };
        window.Show();
        await HeadlessExtensions.FlushAsync();
        return window;
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "agent-up.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not find repository root from {startDirectory}.");
    }

    private sealed class Lookup(params (string Name, string Path)[] entries) : INixOsExecutableLookup
    {
        private readonly Dictionary<string, string> _entries = entries.ToDictionary(
            entry => entry.Name,
            entry => entry.Path,
            StringComparer.Ordinal);

        public string? Find(string executableName)
            => _entries.GetValueOrDefault(executableName);
    }

    private sealed class DockerProvider : IDockerPrerequisiteProvider
    {
        public Task<DockerStatus> CheckAsync(Version minimumVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(new DockerStatus(
                DockerStatusKind.NotInstalled,
                "Docker was not found",
                "Docker is managed through NixOS.",
                null));
    }
}
