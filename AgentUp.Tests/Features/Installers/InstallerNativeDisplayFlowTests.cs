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
    public async Task Installer_completesDryRunFlow_onNativeDisplayBackend()
    {
        _window = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = new InstallerWindow { DataContext = InstallerViewModel.CreateDryRun() };
            window.Show();
            return window;
        });

        await FlushUiAsync();
        await AssertStepAsync("Welcome");

        await AdvanceAsync();
        await AssertStepAsync("License agreement");
        await Dispatcher.UIThread.InvokeAsync(() => _window!.FindControl<CheckBox>("LicenseCheck")!.IsChecked = true);
        await FlushUiAsync();

        await AdvanceAsync();
        await AssertStepAsync("Prerequisite validation");
        await AdvanceAsync();
        await AssertStepAsync("Docker status");
        await AdvanceAsync();
        await AssertStepAsync("Component selection");
        await AdvanceAsync();
        await AssertStepAsync("Installation location");
        await AdvanceAsync();
        await AssertStepAsync("Server configuration");
        await AdvanceAsync();
        await AssertStepAsync("Release payload");
        await AdvanceAsync();
        await AssertStepAsync("Installation summary");
        await AdvanceAsync();
        await AssertStepAsync("Installation complete");

        var body = await Dispatcher.UIThread.InvokeAsync(() => _window!.FindControl<TextBlock>("BodyText")!.Text);
        Assert.That(body, Does.Contain("validated successfully"));
    }

    private async Task AdvanceAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var button = _window!.FindControl<Button>("NextButton")!;
            Assert.That(button.IsEnabled, Is.True);
            button.Command!.Execute(null);
        });
        await FlushUiAsync();
    }

    private async Task AssertStepAsync(string expected)
    {
        var title = await Dispatcher.UIThread.InvokeAsync(() => _window!.FindControl<TextBlock>("StepTitle")!.Text);
        Assert.That(title, Is.EqualTo(expected));
    }

    private static async Task FlushUiAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Task.Delay(50);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }
}
