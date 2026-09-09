using Avalonia.Controls;
using System.Reactive.Linq;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;
namespace AgentUp.Desktop.Tests.Features.Validation.Headless;
public sealed class ValidationPanelTests
{
    [AvaloniaTest]
    public async Task Title_bar_button_opens_server_backed_validation_drawer()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync([WorkspaceFixtures.WithApplications()]);
        var viewModel = (MainViewModel)app.Window.DataContext!;

        await viewModel.ToggleValidationCommand.Execute().FirstAsync();
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsValidationOpen, Is.True);
            Assert.That(app.Window.FindControl<Border>("ValidationPanel")!.IsVisible, Is.True);
            Assert.That(app.Window.FindControl<ItemsControl>("ValidationFlowList"), Is.Not.Null);
        });
    }
}
