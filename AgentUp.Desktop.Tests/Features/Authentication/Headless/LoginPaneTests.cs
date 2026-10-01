using System.Net;
using System.Text;
using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AgentUp.Desktop.Tests.Features.Authentication.Headless;

[TestFixture]
public sealed class LoginPaneTests
{
    [AvaloniaTest]
    public async Task LoginPane_truncatesSavedServerUrlsWithATooltip()
    {
        var window = await LaunchPickerAsync("https://agent-up.massivecreationlab.com");

        var row = window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("au-choice")
                && string.Equals(ToolTip.GetTip(button)?.ToString(), "https://agent-up.massivecreationlab.com", StringComparison.Ordinal));
        var label = row.GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.Classes.Contains("au-choice-label"));

        Assert.Multiple(() =>
        {
            Assert.That(label.Text, Is.EqualTo("https://agent-up.massivecreationlab.com"));
            Assert.That(label.TextWrapping, Is.EqualTo(TextWrapping.NoWrap));
            Assert.That(label.TextTrimming, Is.EqualTo(TextTrimming.CharacterEllipsis));
            Assert.That(row.Classes, Does.Contain("au-choice--compact"));
            Assert.That(
                window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text),
                Does.Not.Contain("Saved sign-in").And.Not.Contain("No saved sign-in").And.Not.Contain("In-app demo"));
        });
    }

    [AvaloniaTest]
    public async Task LoginPane_scrollsTheSavedServerList()
    {
        var window = await LaunchPickerAsync("https://agent-up.massivecreationlab.com");

        var list = window.FindControl<ScrollViewer>("SavedServerList");
        var pane = window.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains("au-sign-in"));

        Assert.Multiple(() =>
        {
            Assert.That(list, Is.Not.Null);
            Assert.That(list!.MaxHeight, Is.EqualTo(168d));
            Assert.That(pane.Width, Is.EqualTo(352d));
            Assert.That(pane.MaxHeight, Is.EqualTo(576d));
        });
    }

    private static async Task<MainWindow> LaunchPickerAsync(string savedUrl)
    {
        var http = new DisposableTestHttpClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"authenticationRequired\":false}", Encoding.UTF8, "application/json")
            });
        var store = new InMemoryServerConnectionStore();
        var controller = AuthenticationTestController.Create(http, store);
        controller.SaveServer(savedUrl, "token-1");
        var login = new LoginViewModel(controller);
        login.ShowPicker();
        var window = new MainWindow(http.Client)
        {
            DataContext = MainViewModelFactory.Create(http.Client, login)
        };
        window.Show();
        await HeadlessExtensions.FlushAsync();
        return window;
    }
}
