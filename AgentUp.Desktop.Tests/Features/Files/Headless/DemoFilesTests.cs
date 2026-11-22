using System.Net.Http;
using System.Reactive.Linq;
using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Features.Authentication.ViewModels;
using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Features.FakeServer.Providers;
using AgentUp.Desktop.Features.FakeServer.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;

namespace AgentUp.Desktop.Tests.Features.Files.Headless;

/// <summary>
/// The Files tab on the built-in Demo: the real window, the production HTTP client, and the
/// in-process Demo backend behind it, so what a person sees after choosing Demo is what runs here.
/// </summary>
[TestFixture]
public sealed class DemoFilesTests
{
    [AvaloniaTest]
    public async Task Demo_files_unfold_on_demand_and_a_saved_edit_shows_up_in_Git()
    {
        var (window, backend) = await LaunchDemoAsync();
        var viewModel = (MainViewModel)window.DataContext!;
        await WaitAsync(() => viewModel.Sidebar.Workspaces.Any(item => item.Id == "bob"));
        viewModel.Sidebar.SelectedWorkspace = viewModel.Sidebar.Workspaces.Single(item => item.Id == "bob");
        await HeadlessExtensions.FlushAsync();

        await window.ClickControlAsync(Find<Control>(window, DesktopAutomationIds.ShellTabFiles));
        await WaitAsync(() => viewModel.Files.Nodes.Any(node => node.Path == "apps"));
        var apps = viewModel.Files.Nodes.Single(node => node.Path == "apps");
        await window.ClickControlAsync(Find<Button>(window, DesktopAutomationIds.FilesToggle("apps")));
        await WaitAsync(() => viewModel.Files.Nodes.Any(node => node.Path == "apps/storefront"));
        await window.ClickControlAsync(Find<Button>(window, DesktopAutomationIds.FilesOpen("README.md")));
        await WaitAsync(() => viewModel.Files.Editor.HasContent);
        window.FindControl<TextBox>("FileEditorText")!.Text += "Edited from Desktop.\n";
        await HeadlessExtensions.FlushAsync();
        await window.ClickControlAsync(window.FindControl<Button>("FileEditorSaveButton")!);
        await WaitAsync(() => viewModel.Files.Editor.StatusLabel == "Modified");

        var changes = backend.Handle(new FakeBackendRequestDtoBuilder().Get("/api/workspaces/bob/git/changes").Build());
        Assert.Multiple(() =>
        {
            Assert.That(apps.ChangedLabel, Is.EqualTo("2"), "a folded folder counts the Demo's changes under it");
            Assert.That(viewModel.Files.Editor.StateLabel, Is.EqualTo("Saved"));
            Assert.That(changes.Body, Does.Contain("README.md"));
        });
    }

    private static async Task<(MainWindow Window, FakeBackendService Backend)> LaunchDemoAsync()
    {
        var backend = FakeServerTestComposition.Backend();
        var fakeServers = FakeServerTestComposition.Controller(backend);
        var http = ServerSessionProvider.CreateClient(
            new Uri("http://127.0.0.1:5000"),
            new FakeServerMessageHandler(backend, new HttpClientHandler()));
        var connections = FakeServerTestComposition.Connections(new InMemoryServerConnectionStore(), http, backend);
        connections.Activate("fake");
        var login = new LoginViewModel(new AuthenticationController(
            new AuthenticationService(new AuthenticationApiClient(http)),
            connections));
        login.ShowPicker();
        login.ServerUrl = FakeServerIdentity.Url;
        await login.ConnectCommand.Execute().FirstAsync();

        var viewModel = MainViewModelFactory.Create(http, login, commandCenter: InterfaceTestComposition.Classic());
        var window = new MainWindow(http, fakeServers) { DataContext = viewModel };
        window.WebViewFactory = () => throw new InvalidOperationException("WebView not available in headless tests");
        window.Show();
        await viewModel.InitializeAsync();
        await HeadlessExtensions.FlushAsync();
        return (window, backend);
    }

    private static T Find<T>(Window window, string automationId) where T : Control
        => window.GetVisualDescendants().OfType<T>()
               .FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == automationId)
           ?? throw new AssertionException($"No {typeof(T).Name} with automation id {automationId}.");

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await HeadlessExtensions.FlushAsync();
            await Task.Delay(10);
        }
    }
}
