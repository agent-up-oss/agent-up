using AgentUp.InstallerApp.Features.Installation.Factories;
using AgentUp.InstallerApp.Features.Installation.ViewModels;
using AgentUp.InstallerApp.Features.Installation.Views;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AgentUp.Tests.Features.Installers;

[TestFixture, Category("E2E")]
public sealed class InstallerNativeDisplayFlowTests
{
    private InstallerWindow? _window;

    [TearDown]
    public async Task TearDown()
    {
        var window = _window;
        _window = null;
        if (window is not null)
            await Dispatcher.UIThread.InvokeAsync(() => window.Close());
    }

    [Test, CancelAfter(60000)]
    public async Task Installer_dashboardInstallsComponentAndCatalogModule_onNativeDisplayBackend()
    {
        _window = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = new InstallerWindow { DataContext = InstallerViewModelFactory.CreateFakeForTests() };
            window.Show();
            return window;
        });

        await FlushUiAsync();
        await AssertPageAsync("Dashboard");

        var model = await Dispatcher.UIThread.InvokeAsync(() => (InstallerViewModel)_window!.DataContext!);
        var cli = model.ComponentCards.Single(card => card.Title == "CLI");
        await Dispatcher.UIThread.InvokeAsync(() => cli.InstallCommand.Execute(null));
        await FlushUiAsync();
        await FlushUiAsync();
        Assert.That(cli.StatusText, Is.EqualTo("Installed"));

        await Dispatcher.UIThread.InvokeAsync(() => _window!.FindControl<Button>("AddModuleCard")!.Command!.Execute(null));
        await FlushUiAsync();
        await AssertPageAsync("AddModule");

        var dotnet = model.CatalogEntries.Single(entry => entry.Entry.Id == "dotnet");
        await Dispatcher.UIThread.InvokeAsync(() => dotnet.InstallCommand.Execute(null));
        await FlushUiAsync();
        await FlushUiAsync();

        await AssertPageAsync("Dashboard");
        Assert.That(model.CapabilityCards.Single(card => card.Id == "dotnet").StatusText, Is.EqualTo("Installed"));
    }

    private async Task AssertPageAsync(string expected)
    {
        var title = await Dispatcher.UIThread.InvokeAsync(() => _window!.FindControl<TextBlock>("PageTitle")!.Text);
        Assert.That(title, Is.EqualTo(expected));
    }

    private static async Task FlushUiAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Task.Delay(50);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }
}
