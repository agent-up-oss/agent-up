using AgentUp.InstallerApp.Features.Installation.ViewModels;
using AgentUp.InstallerApp.Features.Installation.Views;
using AgentUp.InstallerApp.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;

namespace AgentUp.InstallerApp.Tests.Features.Installation.Headless;

[TestFixture]
public class InstallerWindowBehaviorTests
{
    [AvaloniaTest]
    public async Task Window_startsOnWelcomePage()
    {
        var app = await LaunchAsync();

        Assert.That(app.Find<TextBlock>("StepTitle").Text, Is.EqualTo("Welcome"));
        Assert.That(app.Find<Button>("BackButton").IsEnabled, Is.False);
        Assert.That(app.Find<Button>("NextButton").IsEnabled, Is.True);
    }

    [AvaloniaTest]
    public async Task LicensePage_blocksNextUntilLicenseAccepted()
    {
        var app = await LaunchAsync();
        app.Find<Button>("NextButton").Command!.Execute(null);
        await HeadlessExtensions.FlushAsync();

        Assert.That(app.Find<TextBlock>("StepTitle").Text, Is.EqualTo("License agreement"));
        Assert.That(app.Find<Button>("NextButton").IsEnabled, Is.False);

        app.Find<CheckBox>("LicenseCheck").IsChecked = true;
        await HeadlessExtensions.FlushAsync();

        Assert.That(app.Find<Button>("NextButton").IsEnabled, Is.True);
    }

    [AvaloniaTest]
    public async Task FakeAdapter_canCompleteGuidedInstallFlow()
    {
        var app = await LaunchAsync();
        await AdvanceAsync(app); // License
        app.Find<CheckBox>("LicenseCheck").IsChecked = true;

        for (var i = 0; i < 8; i++)
            await AdvanceAsync(app);

        Assert.That(app.Find<TextBlock>("StepTitle").Text, Is.EqualTo("Installation complete"));
        Assert.That(app.Find<TextBlock>("BodyText").Text, Does.Contain("validated successfully"));
    }

    private static async Task<InstallerWindow> LaunchAsync()
    {
        var window = new InstallerWindow { DataContext = InstallerViewModel.CreateFakeForTests() };
        window.Show();
        await HeadlessExtensions.FlushAsync();
        return window;
    }

    private static async Task AdvanceAsync(InstallerWindow window)
    {
        window.Find<Button>("NextButton").Command!.Execute(null);
        await HeadlessExtensions.FlushAsync();
    }
}
