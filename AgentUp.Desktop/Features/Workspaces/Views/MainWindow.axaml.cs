using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Features.Workspaces.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    private readonly Dictionary<string, NativeWebView> _webViews = new();
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
        if (workspaceId != _activeWorkspaceId)
        {
            if (_activeWorkspaceId is not null && _webViews.TryGetValue(_activeWorkspaceId, out var prev))
                prev.IsVisible = false;

            _activeWorkspaceId = workspaceId;

            if (workspaceId is not null && _webViews.TryGetValue(workspaceId, out var existing))
                existing.IsVisible = true;
        }

        NavLog($"After workspace-switch section, url-section? {url is not null && workspaceId is not null}");

        if (url is not null && workspaceId is not null)
        {
            if (!_webViews.TryGetValue(workspaceId, out var webView))
            {
                NavLog($"Creating new NativeWebView for {workspaceId}");
                try
                {
                    webView = new NativeWebView();
                    var capturedId = workspaceId;
                    webView.EnvironmentRequested += (_, e) =>
                    {
                        if (e is GtkWebViewEnvironmentRequestedEventArgs gtk)
                        {
                            var profileRoot = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "agentup", "profiles", capturedId);
                            gtk.BaseDataDirectory = Path.Combine(profileRoot, "data");
                            gtk.BaseCacheDirectory = Path.Combine(profileRoot, "cache");
                        }
                    };
                    NavLog($"new NativeWebView() succeeded for {workspaceId}");
                    _webViews[workspaceId] = webView;
                    PortPane.Children.Add(webView);
                    NavLog($"NativeWebView added to PortPane for {workspaceId}");
                }
                catch (Exception ex)
                {
                    NavLog($"NativeWebView creation FAILED for {workspaceId}: {ex.GetType().Name}: {ex.Message}\n{ex}");
                    return;
                }
            }
            else
            {
                NavLog($"Reusing existing NativeWebView for {workspaceId}");
            }

            webView.IsVisible = true;
            webView.Source = new Uri(url);
            NavLog($"Source set for {workspaceId}: {url}");
        }
        NavLog($"HandleNavigation done ws={workspaceId}");
    }
}
