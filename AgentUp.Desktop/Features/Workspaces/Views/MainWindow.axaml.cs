using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Features.Workspaces.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    private readonly Dictionary<string, NativeWebView> _webViews = new();
    private readonly Dictionary<string, string> _webViewErrors = new();
    private readonly Dictionary<string, string> _lastKnownBrowserUrls = new();
    private readonly DispatcherTimer _addressPollTimer;
    private string? _activeWorkspaceId;

    // Overrideable in tests to inject a factory that throws without needing GTK.
    internal Func<NativeWebView> WebViewFactory { get; set; } = () => new NativeWebView();

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        _addressPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _addressPollTimer.Tick += (_, _) => _ = PollActiveBrowserAddressAsync();
        _addressPollTimer.Start();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MainViewModel vm) return;
        vm.BrowserNavigation.Subscribe(nav =>
            Dispatcher.UIThread.Post(() => HandleNavigation(nav.WorkspaceId, nav.Url)));
        vm.BrowserCommands.Subscribe(command =>
            Dispatcher.UIThread.Post(() => HandleBrowserCommand(command)));
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
                    webView.PropertyChanged += (_, e) =>
                    {
                        if (e.Property.Name != nameof(NativeWebView.Source)) return;
                        UpdateAddressFromWebView(capturedId, webView.Source);
                    };
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
                        UpdateAddressFromWebView(capturedId, uri);
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
            var destination = new Uri(url);
            if (!isNew && string.Equals(webView.Source?.ToString(), destination.ToString(), StringComparison.Ordinal))
            {
                webView.Source = new Uri("about:blank");
                Dispatcher.UIThread.Post(() => webView.Source = destination, DispatcherPriority.Background);
            }
            else
            {
                webView.Source = destination;
            }
        }
    }

    private void HandleBrowserCommand(BrowserCommand command)
    {
        if (_activeWorkspaceId is null) return;
        if (!_webViews.TryGetValue(_activeWorkspaceId, out var webView)) return;

        switch (command)
        {
            case BrowserCommand.Back:
                if (webView.CanGoBack)
                    webView.GoBack();
                break;
            case BrowserCommand.Forward:
                if (webView.CanGoForward)
                    webView.GoForward();
                break;
            case BrowserCommand.Reload:
                var url = (DataContext as MainViewModel)?.AddressBarUrl ?? webView.Source?.ToString();
                if (!string.IsNullOrWhiteSpace(url))
                    HandleNavigation(_activeWorkspaceId, url);
                break;
        }
    }

    private void UpdateAddressFromWebView(string workspaceId, Uri? uri)
    {
        if (uri is null) return;
        if (uri.Scheme is not ("http" or "https")) return;

        UpdateAddressFromWebView(workspaceId, uri.ToString());
    }

    private void UpdateAddressFromWebView(string workspaceId, string navigatedUrl)
    {
        var previousUrl = _lastKnownBrowserUrls.GetValueOrDefault(workspaceId);
        _lastKnownBrowserUrls[workspaceId] = navigatedUrl;
        BrowserUrlStore.Write(workspaceId, navigatedUrl);

        if (workspaceId != _activeWorkspaceId || DataContext is not MainViewModel vm)
            return;

        var addressHasUserEdit =
            FocusManager?.GetFocusedElement() == AddressBar
            && !string.Equals(vm.AddressBarUrl, previousUrl, StringComparison.Ordinal);

        if (!addressHasUserEdit)
            vm.UpdateAddressFromBrowser(workspaceId, navigatedUrl);
    }

    private async Task PollActiveBrowserAddressAsync()
    {
        if (_activeWorkspaceId is null) return;
        if (DataContext is not MainViewModel { ShowPortView: true }) return;
        if (!_webViews.TryGetValue(_activeWorkspaceId, out var webView)) return;
        if (!webView.IsVisible) return;

        try
        {
            var result = await webView.InvokeScript("window.location.href");
            var url = TryReadHttpLocation(result);
            if (url is not null)
                UpdateAddressFromWebView(_activeWorkspaceId, url);
        }
        catch
        {
            // The page may still be loading or the native WebView may not be ready yet.
        }
    }

    internal static string? TryReadHttpLocation(string? scriptResult)
    {
        if (string.IsNullOrWhiteSpace(scriptResult)) return null;

        var candidate = scriptResult.Trim();
        if (candidate.Length >= 2 && candidate[0] == '"' && candidate[^1] == '"')
            candidate = candidate[1..^1].Replace("\\/", "/").Replace("\\\"", "\"");

        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
               && uri.Scheme is "http" or "https"
            ? uri.ToString()
            : null;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (FocusManager?.GetFocusedElement() != AddressBar) return;
        if (e.Source is not Visual source) return;
        if (ReferenceEquals(source, AddressBar) || AddressBar.IsVisualAncestorOf(source)) return;

        FocusSink.Focus(NavigationMethod.Pointer);
    }

    private void OnWindowChromePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is Visual source && IsWindowControlSource(source)) return;

        if (e.ClickCount == 2)
            ToggleMaximized();
        else
            BeginMoveDrag(e);
    }

    private bool IsWindowControlSource(Visual source)
        => CloseWindowButton.IsVisualAncestorOf(source)
           || MinimizeWindowButton.IsVisualAncestorOf(source)
           || RestoreWindowButton.IsVisualAncestorOf(source)
           || SidebarToggle.IsVisualAncestorOf(source)
           || ReloadButton.IsVisualAncestorOf(source);

    private void OnCloseWindowClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnMinimizeWindowClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnRestoreWindowClicked(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void ToggleMaximized()
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
}
