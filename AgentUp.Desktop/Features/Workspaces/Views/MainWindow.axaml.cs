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

    // Evaluates a JavaScript expression in the WebView for the given workspace and returns
    // the result as a string. Returns null if no WebView exists for that workspace yet.
    internal async Task<string?> EvalAsync(string workspaceId, string script)
    {
        if (!_webViews.TryGetValue(workspaceId, out var webView)) return null;
        // InvokeScript is synchronous but touches UI state — run it on the UI thread.
        return await Dispatcher.UIThread.InvokeAsync(() => webView.InvokeScript(script));
    }

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
            var isNew = !_webViews.TryGetValue(workspaceId, out var webView);
            if (isNew)
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
                    var firstNavDone = false;
                    webView.NavigationCompleted += (_, e) =>
                    {
                        if (!e.IsSuccess || e.Request is not { } uri) return;
                        BrowserUrlStore.Write(capturedId, uri.ToString());
                        if (firstNavDone) return;
                        firstNavDone = true;
                        // WebKit renders content into the native window but GTK only composites
                        // it when XMapWindow fires an Expose event on the embedded window.
                        // The window was already mapped before the first navigation completed,
                        // so no Expose arrives — content loads but stays invisible.
                        // A hide→show at separate dispatcher priorities forces XMapWindow,
                        // which sends the Expose that causes WebKit to repaint.
                        // Background priority ensures the show runs after layout/render have
                        // already processed the hide (unmapping the native window first).
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (!webView.IsVisible) return;
                            webView.IsVisible = false;
                            Dispatcher.UIThread.Post(
                                () => webView.IsVisible = true,
                                DispatcherPriority.Background);
                        });
                    };
                    _webViews[workspaceId] = webView;
                    _webViewErrors.Remove(workspaceId);

                    // Restore last-visited URL before the first navigation.
                    url = BrowserUrlStore.Read(workspaceId, url) ?? url;

                    PortPane.Children.Add(webView);
                    UpdateErrorDisplay(workspaceId);
                }
                catch (Exception ex)
                {
                    _webViewErrors[workspaceId] = $"Could not start the browser: {ex.Message}";
                    UpdateErrorDisplay(workspaceId);
                    return;
                }
            }

            webView!.IsVisible = true;
            webView.Source = new Uri(url);
        }
    }
}
