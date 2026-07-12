using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using AvaloniaWebView;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Features.Workspaces.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    // One WebView per workspace — created lazily on first port-tab selection.
    // Each is kept alive so its session persists while the app runs.
    private readonly Dictionary<string, WebView> _webViews = new();
    private string? _activeWorkspaceId;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is not MainViewModel vm) return;

        vm.BrowserNavigation.Subscribe(nav =>
            Dispatcher.UIThread.Post(() => HandleNavigation(nav.WorkspaceId, nav.Url)));
    }

    internal void NavigateTo(string workspaceId, string url) => HandleNavigation(workspaceId, url);

    private static void NavLog(string msg)
    {
        System.IO.File.AppendAllText("/tmp/agentup-nav.log", msg + "\n");
    }

    private void HandleNavigation(string? workspaceId, string? url)
    {
        NavLog($"HandleNavigation ws={workspaceId} url={url}");
        // When the active workspace changes, show/hide the cached WebView for it.
        if (workspaceId != _activeWorkspaceId)
        {
            if (_activeWorkspaceId is not null && _webViews.TryGetValue(_activeWorkspaceId, out var prev))
                prev.IsVisible = false;

            _activeWorkspaceId = workspaceId;

            if (workspaceId is not null && _webViews.TryGetValue(workspaceId, out var existing))
                existing.IsVisible = true;
        }

        NavLog($"After workspace-switch section, url-section? {url is not null && workspaceId is not null}");

        // A port tab was selected — create the WebView lazily and navigate.
        if (url is not null && workspaceId is not null)
        {
            if (!_webViews.TryGetValue(workspaceId, out var webView))
            {
                NavLog($"Creating new WebView for {workspaceId}");
                try
                {
                    webView = new WebView();
                    NavLog($"new WebView() succeeded for {workspaceId}");
                    _webViews[workspaceId] = webView;
                    PortPane.Children.Add(webView);
                    NavLog($"WebView added to PortPane for {workspaceId}");
                }
                catch (Exception ex)
                {
                    NavLog($"WebView creation FAILED for {workspaceId}: {ex.GetType().Name}: {ex.Message}\n{ex}");
                    return;
                }
            }
            else
            {
                NavLog($"Reusing existing WebView for {workspaceId}");
            }

            webView.IsVisible = true;
            webView.Url = new Uri(url);
            NavLog($"Url set for {workspaceId}: {url}");
        }
        NavLog($"HandleNavigation done ws={workspaceId}");
    }
}
