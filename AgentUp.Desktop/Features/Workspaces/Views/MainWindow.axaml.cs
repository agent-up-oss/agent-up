using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Features.Workspaces.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    private readonly Dictionary<string, NativeWebView> _webViews = new();
    private readonly Dictionary<string, string> _webViewErrors = new();
    private string? _activeWorkspaceId;

    // Overrideable in tests to inject a factory that throws without needing GTK.
    internal Func<NativeWebView> WebViewFactory { get; set; } = () => new NativeWebView();

    public MainWindow() { InitializeComponent(); }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MainViewModel vm) return;
        vm.BrowserNavigation.Subscribe(nav =>
            Dispatcher.UIThread.Post(() => HandleNavigation(nav.WorkspaceId, nav.Url)));
    }

    internal void NavigateTo(string workspaceId, string? url) => HandleNavigation(workspaceId, url);

    private void UpdateErrorDisplay(string? workspaceId)
    {
        if (workspaceId is not null && _webViewErrors.TryGetValue(workspaceId, out var error))
        {
            WebViewErrorText.Text = error;
            WebViewErrorBanner.IsVisible = true;
        }
        else
        {
            WebViewErrorBanner.IsVisible = false;
        }
    }

    private void HandleNavigation(string? workspaceId, string? url)
    {
        if (workspaceId != _activeWorkspaceId)
        {
            if (_activeWorkspaceId is not null && _webViews.TryGetValue(_activeWorkspaceId, out var prev))
                prev.IsVisible = false;

            _activeWorkspaceId = workspaceId;

            if (workspaceId is not null && _webViews.TryGetValue(workspaceId, out var existing))
                existing.IsVisible = true;

            UpdateErrorDisplay(workspaceId);
        }

        if (url is not null && workspaceId is not null)
        {
            if (!_webViews.TryGetValue(workspaceId, out var webView))
            {
                try
                {
                    webView = WebViewFactory();
                    var capturedId = workspaceId;
                    webView.EnvironmentRequested += (_, e) =>
                    {
                        if (e is GtkWebViewEnvironmentRequestedEventArgs gtk)
                        {
                            var profileRoot = BrowserUrlStore.ProfilePath(capturedId);
                            gtk.BaseDataDirectory = Path.Combine(profileRoot, "data");
                            gtk.BaseCacheDirectory = Path.Combine(profileRoot, "cache");
                        }
                    };
                    webView.NavigationCompleted += (_, e) =>
                    {
                        if (e.IsSuccess && e.Request is { } uri)
                            BrowserUrlStore.Write(capturedId, uri.ToString());
                    };
                    _webViews[workspaceId] = webView;
                    _webViewErrors.Remove(workspaceId);
                    PortPane.Children.Add(webView);
                    UpdateErrorDisplay(workspaceId);

                    // On first load, restore the last-visited page if it's on the same port.
                    url = BrowserUrlStore.Read(workspaceId, url) ?? url;
                }
                catch (Exception ex)
                {
                    _webViewErrors[workspaceId] = $"Could not start the browser: {ex.Message}";
                    UpdateErrorDisplay(workspaceId);
                    return;
                }
            }

            webView.IsVisible = true;
            webView.Source = new Uri(url);
        }
    }
}
