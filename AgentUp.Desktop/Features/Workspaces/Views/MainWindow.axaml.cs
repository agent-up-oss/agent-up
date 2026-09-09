using System.Diagnostics;
using System.Collections.Specialized;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.Audit.Controllers;
using AgentUp.Desktop.Features.Metrics.Controllers;
using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Browser.DTOs;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    // One NativeWebView per HTTP port tab — keyed by "workspaceId:{port}".
    // Switching between workspace tabs only toggles IsVisible; the WebView is never navigated away,
    // preserving full page state (scroll position, open accordions, JS memory, auth session).
    private readonly Dictionary<string, NativeWebView> _webViews = new();
    // OAuth/sign-in popups opened via window.open() from within a workspace WebView — keyed by "{tabKey}:{sequence}".
    private readonly Dictionary<string, IWebPopup> _webPopups = new();
    private int _popupSequence;
    // Errors keyed by workspaceId (not tabKey) so the banner persists across tab switches.
    private readonly Dictionary<string, string> _webViewErrors = new();
    // Last successfully navigated http URL per tabKey; absent means tab is in error state.
    private readonly Dictionary<string, string> _lastKnownBrowserUrls = new();
    private readonly Dictionary<string, int> _navigationVersions = new();
    private readonly CompositeDisposable _subscriptions = new();
    private readonly DispatcherTimer _addressPollTimer;
    private readonly HttpClient _serverHttp;
    private readonly string _serverBaseUrl;
    private WorkspaceEventClient? _workspaceEventClient;
    private string? _activeWorkspaceId;
    private string? _activeTabKey;
    private bool _isClosed;
    private NativeWebView? _consoleWebView;
    private Panel? _consoleOverlay;
    private bool _consoleSelecting;
    private ViewModelAuditController? _auditController;
    private HostMetricsController? _hostMetricsController;
    private const int ConsoleDefaultDisplayLines = 2_000;
    private static readonly HttpClient PortProbeHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    internal Func<NativeWebView> WebViewFactory { get; set; } = () => new NativeWebView();
    internal Func<IWebPopup> WebPopupFactory { get; set; } = () => new NativeWebDialogPopup();
    internal int OpenPopupCountForTests => _webPopups.Count;
    internal Func<Uri, Task<string?>> BrowserProbe { get; set; } = ProbeBrowserDestinationAsync;
    // Seam over the native file dialog. No test runner can drive a GTK/AppKit/Win32 file
    // chooser, so end-to-end tests substitute the chooser step and keep every other part of
    // the upload bridge — script injection, the WebView message, IStorageFile reads, and the
    // completion script — running against the real platform WebView and storage provider.
    internal Func<FilePickerOpenOptions, Task<IReadOnlyList<IStorageFile>>> FilePicker { get; set; }
    internal BrowserViewportController BrowserViewport { get; }
    internal WebViewFilePickerController BrowserFilePicker { get; }
    internal bool HasBrowserResourcesForTests =>
        _addressPollTimer.IsEnabled
        || HasWorkspaceBrowserResourcesForTests
        || _activeWorkspaceId is not null;
    internal bool HasWorkspaceBrowserResourcesForTests =>
        _webViews.Count > 0
        || _webViewErrors.Count > 0
        || _lastKnownBrowserUrls.Count > 0
        || _activeWorkspaceId is not null
        || _activeTabKey is not null;

    internal bool ArePortWebViewsHiddenForTests =>
        _webViews.Count == 0 || _webViews.Values.All(webView => !webView.IsVisible);

    internal bool IsConsoleWebViewHiddenForTests =>
        _consoleWebView is null || !_consoleWebView.IsVisible;

    private const string SelectionJs =
        "(function(){" +
        "if(!document.getElementById('_au_sel')){" +
        "var st=document.createElement('style');st.id='_au_sel';" +
        "st.textContent='::selection{background-color:#0f7a45!important;color:#f5fbf7!important}';" +
        "(document.head||document.documentElement).appendChild(st);}" +
        "var active=false;" +
        "window._selStart=function(x,y){" +
        "active=true;" +
        "var r=document.caretRangeFromPoint(x,y);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "s.removeAllRanges();" +
        "var g=document.createRange();" +
        "g.setStart(r.startContainer,r.startOffset);" +
        "g.collapse(true);" +
        "s.addRange(g);" +
        "};" +
        "window._selEnd=function(){active=false;};" +
        "document.addEventListener('mousemove',function(e){" +
        "if(!active)return;" +
        "var r=document.caretRangeFromPoint(e.clientX,e.clientY);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "if(!s.anchorNode)return;" +
        "try{s.extend(r.startContainer,r.startOffset);}catch(ex){}" +
        "},true);" +
        "})();";

    private const string ConsoleJs =
        "(function(){" +
        "function focus(){var c=document.getElementById('content');if(c)c.focus({preventScroll:true});}" +
        "window._selStart=function(x,y){" +
        "focus();" +
        "var r=document.caretRangeFromPoint(x,y);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "s.removeAllRanges();" +
        "var g=document.createRange();" +
        "g.setStart(r.startContainer,r.startOffset);" +
        "g.collapse(true);" +
        "s.addRange(g);" +
        "};" +
        "window._selExtend=function(x,y){" +
        "var r=document.caretRangeFromPoint(x,y);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "if(!s.anchorNode)return;" +
        "try{s.extend(r.startContainer,r.startOffset);}catch(ex){}" +
        "};" +
        "window._selWord=function(x,y){" +
        "focus();" +
        "var r=document.caretRangeFromPoint(x,y);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "s.removeAllRanges();s.addRange(r);" +
        "s.modify('expand','backward','word');" +
        "s.modify('extend','forward','word');" +
        "};" +
        "window._selLine=function(x,y){" +
        "focus();" +
        "var r=document.caretRangeFromPoint(x,y);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "s.removeAllRanges();s.addRange(r);" +
        "s.modify('expand','backward','lineboundary');" +
        "s.modify('extend','forward','lineboundary');" +
        "};" +
        "window._scroll=function(dy){" +
        "var c=document.getElementById('content');" +
        "if(c)c.scrollTop+=dy;" +
        "};" +
        "})();";

    public MainWindow() : this(CreateServerHttpClient())
    {
    }

    private static HttpClient CreateServerHttpClient() => new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL") ?? "http://localhost:5000")
    };

    public MainWindow(HttpClient serverHttp)
    {
        InitializeComponent();
        SetWindowIcon();
        BrowserViewport = new BrowserViewportController(NavigateTo, EvalAsync);
        BrowserFilePicker = new WebViewFilePickerController();
        FilePicker = options => StorageProvider.OpenFilePickerAsync(options);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        _addressPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _addressPollTimer.Tick += OnAddressPollTimerTick;
        PortPane.SizeChanged += OnPortPaneSizeChanged;
        _serverBaseUrl = serverHttp.BaseAddress?.ToString().TrimEnd('/')
            ?? throw new ArgumentException("The server HTTP client requires a base address.", nameof(serverHttp));
        _serverHttp = serverHttp;
    }

    private void SetWindowIcon()
    {
        try
        {
            var iconPath = FindWindowIconPath();
            if (iconPath is null) return;

            using var stream = File.OpenRead(iconPath);
            Icon = new WindowIcon(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }

    private static string? FindWindowIconPath()
    {
        var outputPath = Path.Join(AppContext.BaseDirectory, "media", "logo.png");
        if (File.Exists(outputPath)) return outputPath;

        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            var candidate = Path.Join(dir, "media", "logo.png");
            if (File.Exists(candidate)) return candidate;

            var parent = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (parent == dir) break;
            dir = parent;
        }

        return null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        _workspaceEventClient?.Dispose();
        _workspaceEventClient = null;

        base.OnDataContextChanged(e);
        if (DataContext is not MainViewModel vm) return;

        _subscriptions.Clear();
        vm.BrowserNavigation.Subscribe(nav =>
            Dispatcher.UIThread.Post(() => HandleNavigation(nav.WorkspaceId, nav.Url, reloadIfSameUrl: true)))
            .DisposeWith(_subscriptions);
        vm.BrowserTabNavigation.Subscribe(nav =>
            Dispatcher.UIThread.Post(() => HandleNavigation(nav.WorkspaceId, nav.Url, reloadIfSameUrl: false)))
            .DisposeWith(_subscriptions);
        vm.BrowserCommands.Subscribe(command =>
            Dispatcher.UIThread.Post(() => HandleBrowserCommand(command)))
            .DisposeWith(_subscriptions);
        vm.Sidebar.Workspaces.CollectionChanged += OnWorkspaceCollectionChanged;
        Disposable.Create(() => vm.Sidebar.Workspaces.CollectionChanged -= OnWorkspaceCollectionChanged)
            .DisposeWith(_subscriptions);
        vm.Tutorial.WhenAnyValue(t => t.IsVisible)
            .CombineLatest(
                vm.Sidebar.DeleteConfirmation.WhenAnyValue(d => d.IsVisible),
                vm.Sidebar.AddWorkspace.WhenAnyValue(a => a.IsVisible),
                vm.Git.Diff.WhenAnyValue(d => d.IsVisible),
                (tutorialVisible, deleteVisible, addVisible, diffVisible) =>
                    tutorialVisible || deleteVisible || addVisible || diffVisible)
            .DistinctUntilChanged()
            .Subscribe(modalVisible =>
                Dispatcher.UIThread.Post(() => ApplyModalOverlayWebViewVisibility(modalVisible)))
            .DisposeWith(_subscriptions);
        ApplyModalOverlayWebViewVisibility(IsModalOverlayVisible());
        vm.Console.WhenAnyValue(c => c.IsLoading)
            .Skip(1)
            .Where(loading => !loading)
            .Where(_ => vm.ShowConsole)
            .Subscribe(_ => Dispatcher.UIThread.Post(RefreshConsoleWebView))
            .DisposeWith(_subscriptions);
        vm.Console.Lines.CollectionChanged += OnConsoleLinesChanged;
        Disposable.Create(() => vm.Console.Lines.CollectionChanged -= OnConsoleLinesChanged)
            .DisposeWith(_subscriptions);
        vm.WhenAnyValue(v => v.ShowConsole)
            .Where(visible => visible && !vm.Console.IsLoading)
            .Subscribe(_ => Dispatcher.UIThread.Post(RefreshConsoleWebView))
            .DisposeWith(_subscriptions);
        vm.Console.WhenAnyValue(c => c.ShowAllLines)
            .Where(all => all && vm.ShowConsole)
            .Subscribe(_ => Dispatcher.UIThread.Post(RefreshConsoleWebView))
            .DisposeWith(_subscriptions);
        vm.WhenAnyValue(v => v.ShowPortView)
            .Skip(1)
            .DistinctUntilChanged()
            .Where(show => show)
            .Subscribe(_ => Dispatcher.UIThread.Post(WakeActiveWebView))
            .DisposeWith(_subscriptions);
        vm.WhenAnyValue(v => v.ShowPortView)
            .Subscribe(show => Dispatcher.UIThread.Post(() =>
            {
                if (show)
                    _addressPollTimer.Start();
                else
                    _addressPollTimer.Stop();
            }))
            .DisposeWith(_subscriptions);
        if (vm.ShowPortView)
            _addressPollTimer.Start();

        _hostMetricsController?.Dispose();
        _hostMetricsController = MainViewModelFactory.CreateHostMetricsController(_serverHttp);
        _hostMetricsController.Start();

        _auditController ??= new ViewModelAuditController(_serverHttp);
        _auditController.Attach(vm, CaptureViewState);
    }

    internal void StartAuthenticatedServices()
    {
        if (DataContext is not MainViewModel vm)
            return;

        _workspaceEventClient?.Dispose();
        var eventHttp = new HttpClient { BaseAddress = _serverHttp.BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
        eventHttp.DefaultRequestHeaders.Authorization = _serverHttp.DefaultRequestHeaders.Authorization;
        _workspaceEventClient = new WorkspaceEventClient(eventHttp, vm.Sidebar);
        _workspaceEventClient.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _workspaceEventClient?.Dispose();
        _auditController?.Dispose();
        _hostMetricsController?.Dispose();
        _serverHttp.Dispose();
        _addressPollTimer.Stop();
        _addressPollTimer.Tick -= OnAddressPollTimerTick;
        _subscriptions.Dispose();
        DestroyWorkspaceWebViews();
        DestroyConsoleWebView();
        base.OnClosed(e);
    }

    private IReadOnlyDictionary<string, string> CaptureViewState()
    {
        if (Dispatcher.UIThread.CheckAccess())
            return CaptureCoreOnUiThread();
        try
        {
            return Dispatcher.UIThread.Invoke(CaptureCoreOnUiThread);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or TaskCanceledException)
        {
            return new Dictionary<string, string> { ["webView.captureError"] = ex.Message };
        }
    }

    private Dictionary<string, string> CaptureCoreOnUiThread()
    {
        var f = new Dictionary<string, string>
        {
            ["webView.activeWorkspaceId"] = _activeWorkspaceId ?? string.Empty,
            ["webView.activeTabKey"] = _activeTabKey ?? string.Empty,
            ["webView.webViewCount"] = _webViews.Count.ToString(),
            ["webView.hasConsoleWebView"] = (_consoleWebView is not null).ToString(),
            ["webView.windowState"] = WindowState.ToString(),
            ["webView.isClosed"] = _isClosed.ToString(),
            ["webView.addressPollTimerEnabled"] = _addressPollTimer.IsEnabled.ToString(),
            ["webView.errorCount"] = _webViewErrors.Count.ToString(),
            ["webView.errors"] = string.Join("; ", _webViewErrors.Select(kv => $"{kv.Key}={kv.Value}")),
            ["webView.lastKnownUrlCount"] = _lastKnownBrowserUrls.Count.ToString(),
            ["webView.lastKnownUrls"] = string.Join("; ", _lastKnownBrowserUrls.Select(kv => $"{kv.Key}={kv.Value}")),
            ["webView.tabKeys"] = string.Join(", ", _webViews.Keys),
        };

        if (_activeTabKey is not null && _webViews.TryGetValue(_activeTabKey, out var activeWv))
            f["webView.activeSourceUrl"] = activeWv.Source?.ToString() ?? string.Empty;
        else
            f["webView.activeSourceUrl"] = string.Empty;

        if (_activeTabKey is not null && _webViews.TryGetValue(_activeTabKey, out var activeMarginWv))
        {
            f["webView.activeMargin"] = activeMarginWv.Margin.ToString();
            f["webView.activeBounds"] = $"{activeMarginWv.Bounds.Width:F0}x{activeMarginWv.Bounds.Height:F0}";
            f["webView.activeDesiredSize"] = $"{activeMarginWv.DesiredSize.Width:F0}x{activeMarginWv.DesiredSize.Height:F0}";
            f["webView.activeIsHitTestVisible"] = activeMarginWv.IsHitTestVisible.ToString();
            f["webView.activeMaxSize"] = $"{activeMarginWv.MaxWidth}x{activeMarginWv.MaxHeight}";
        }
        else
        {
            f["webView.activeMargin"] = "";
            f["webView.activeBounds"] = "";
            f["webView.activeDesiredSize"] = "";
            f["webView.activeIsHitTestVisible"] = "";
            f["webView.activeMaxSize"] = "";
        }

        return f;
    }

    private void OnWorkspaceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isClosed) return;

        if (e.Action == NotifyCollectionChangedAction.Reset
            || sender is System.Collections.ICollection { Count: 0 })
        {
            DestroyWorkspaceWebViews();
            return;
        }

        if (e.OldItems is null) return;
        foreach (var item in e.OldItems.OfType<WorkspaceItemViewModel>())
            DestroyWorkspaceWebViews(item.Id);
    }

    internal void NavigateTo(string workspaceId, string? url) => HandleNavigation(workspaceId, url, reloadIfSameUrl: true);

    internal async Task<string?> EvalAsync(string workspaceId, string script)
    {
        var result = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var tabKey = ResolveEvaluationTabKey(workspaceId);
            if (tabKey is null) return null;
            if (!_webViews.TryGetValue(tabKey, out var webView)) return null;
            return await webView.InvokeScript(script);
        });
        return NormalizeScriptResult(result);
    }

    private string? ResolveEvaluationTabKey(string workspaceId)
        => _activeWorkspaceId == workspaceId ? _activeTabKey : null;

    private void UpdateErrorDisplay(string? workspaceId)
    {
        if (IsModalOverlayVisible())
        {
            WebViewErrorBanner.IsVisible = false;
            return;
        }

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

    private void HandleNavigation(string? workspaceId, string? url, bool reloadIfSameUrl)
    {
        if (_isClosed || workspaceId is null) return;
        if (url is null)
        {
            if (workspaceId == _activeWorkspaceId) return;

            if (_activeTabKey is not null && _webViews.TryGetValue(_activeTabKey, out var previous))
                previous.IsVisible = false;

            _activeWorkspaceId = workspaceId;
            _activeTabKey = null;
            UpdateErrorDisplay(workspaceId);
            return;
        }

        HandleDirectNavigation(workspaceId, url, IsModalOverlayVisible(), reloadIfSameUrl);
    }

    private void HandleDirectNavigation(string? workspaceId, string? url, bool modalOverlayVisible, bool reloadIfSameUrl)
    {
        if (workspaceId is null || url is null) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var navUri) || navUri.Scheme is not ("http" or "https")) return;

        var tabKey = TabKey(workspaceId, navUri);
        ActivateTab(workspaceId, tabKey, modalOverlayVisible);

        if (_webViews.TryGetValue(tabKey, out var existingWebView))
        {
            existingWebView.IsVisible = !modalOverlayVisible;
            if (!reloadIfSameUrl && !ShouldNavigateExistingWebView(_lastKnownBrowserUrls.GetValueOrDefault(tabKey), url))
                return;
            var errNavVer = _navigationVersions.GetValueOrDefault(tabKey) + 1;
            _navigationVersions[tabKey] = errNavVer;
            _ = NavigatePortWebViewAsync(tabKey, workspaceId, existingWebView, navUri, errNavVer);
            return;
        }

        if (!TryGetOrCreateWebView(tabKey, workspaceId, url, out var webView, out var destinationUrl)) return;
        webView.IsVisible = !modalOverlayVisible;
        var navigationVersion = _navigationVersions.GetValueOrDefault(tabKey) + 1;
        _navigationVersions[tabKey] = navigationVersion;
        _ = NavigatePortWebViewAsync(tabKey, workspaceId, webView, new Uri(destinationUrl), navigationVersion);
    }

    private static string TabKey(string workspaceId, Uri uri) => $"{workspaceId}:{uri.Port}";

    internal static bool ShouldNavigateExistingWebView(string? lastKnownUrl, string requestedUrl)
        => lastKnownUrl is null || !string.Equals(lastKnownUrl, requestedUrl, StringComparison.Ordinal);

    private void ActivateTab(string? workspaceId, string? tabKey, bool modalOverlayVisible)
    {
        if (workspaceId == _activeWorkspaceId && tabKey == _activeTabKey) return;

        if (_activeTabKey is not null && _webViews.TryGetValue(_activeTabKey, out var previous))
            previous.IsVisible = false;

        _activeWorkspaceId = workspaceId;
        _activeTabKey = tabKey;

        if (!modalOverlayVisible && tabKey is not null && _webViews.TryGetValue(tabKey, out var next))
            next.IsVisible = true;

        UpdateErrorDisplay(workspaceId);
    }

    private bool TryGetOrCreateWebView(
        string tabKey,
        string workspaceId,
        string requestedUrl,
        out NativeWebView webView,
        out string destinationUrl)
    {
        destinationUrl = requestedUrl;
        if (_webViews.TryGetValue(tabKey, out webView!))
            return true;

        try
        {
            webView = CreateWorkspaceWebView(tabKey, workspaceId);
            _webViews[tabKey] = webView;
            _webViewErrors.Remove(workspaceId);
            webView.IsVisible = false;
            PortPane.Children.Add(webView);
            UpdateErrorDisplay(workspaceId);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _webViewErrors[workspaceId] = $"Could not start the browser: {ex.Message}";
            UpdateErrorDisplay(workspaceId);
            return false;
        }
    }

    private NativeWebView CreateWorkspaceWebView(string tabKey, string workspaceId)
    {
        var webView = WebViewFactory();
        var firstNavDone = false;

        webView.NavigationCompleted += (_, e) =>
        {
            var url = e.Request?.ToString() ?? string.Empty;
            if (!e.IsSuccess)
            {
                RecordWebViewEvent(workspaceId, "navigation_error", "error", new()
                {
                    ["tabKey"] = tabKey,
                    ["url"] = url,
                    ["isVisible"] = webView.IsVisible.ToString(),
                });

                if (e.Request is { } failedUri && failedUri.Scheme is "http" or "https")
                {
                    ShowBrowserErrorPage(
                        tabKey,
                        workspaceId,
                        webView,
                        failedUri,
                        "Could not load page",
                        "The embedded browser could not load this route.");
                }
                return;
            }

            RecordWebViewEvent(workspaceId, "navigation_complete", "success", new()
            {
                ["tabKey"] = tabKey,
                ["url"] = url,
                ["isVisible"] = webView.IsVisible.ToString(),
            });

            _ = webView.InvokeScript(SelectionJs);
            _ = webView.InvokeScript(BrowserFilePicker.InstallScript);
            if (firstNavDone) return;
            firstNavDone = true;
            ForceFirstWebKitPaint(tabKey, webView);
        };

        webView.NewWindowRequested += (_, e) => HandleNewWindowRequested(workspaceId, tabKey, e);
        webView.WebMessageReceived += (_, e) =>
        {
            if (BrowserFilePicker.TryParseRequest(e.Body, out var request) && request is not null)
                _ = HandleFilePickerRequestAsync(tabKey, webView, request);
        };

        return webView;
    }

    // Many OAuth/sign-in flows (Google, GitHub, Microsoft, Auth0, ...) launch via window.open()
    // rather than a same-tab redirect. Without handling this, the popup silently fails to open
    // and the flow appears to just do nothing. We open the requested URL in a NativeWebDialog —
    // a separate native-webview-backed window sharing the same engine/cookie store — so the
    // popup renders and the sign-in flow can complete.
    private void HandleNewWindowRequested(string workspaceId, string tabKey, WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (_isClosed) return;
        if (e.Request is not { Scheme: "http" or "https" } popupUri) return;

        var popupId = $"{tabKey}:{++_popupSequence}";
        try
        {
            var popup = WebPopupFactory();
            _webPopups[popupId] = popup;
            popup.Title = "Sign in";
            popup.Closing += (_, _) => ClosePopup(popupId);
            popup.Navigate(popupUri);
            popup.Show();

            RecordWebViewEvent(workspaceId, "popup_opened", "success", new()
            {
                ["tabKey"] = tabKey,
                ["url"] = popupUri.ToString(),
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _webPopups.Remove(popupId);
            RecordWebViewEvent(workspaceId, "popup_open_failed", "error", new()
            {
                ["tabKey"] = tabKey,
                ["url"] = popupUri.ToString(),
                ["error"] = ex.Message,
            });
            Trace.TraceWarning($"Could not open sign-in popup: {ex.Message}");
        }
    }

    private void ClosePopup(string popupId)
    {
        if (!_webPopups.Remove(popupId, out var popup)) return;
        try { popup.Dispose(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
    }

    private void ClosePopups(string tabKeyOrWorkspacePrefix)
    {
        foreach (var popupId in _webPopups.Keys.Where(key => key.StartsWith($"{tabKeyOrWorkspacePrefix}:", StringComparison.Ordinal)).ToList())
            ClosePopup(popupId);
    }

    private async Task HandleFilePickerRequestAsync(
        string tabKey,
        NativeWebView webView,
        WebViewFilePickerRequest request)
    {
        try
        {
            var files = await FilePicker(new FilePickerOpenOptions
            {
                AllowMultiple = request.Multiple,
                Title = request.Multiple ? "Choose files to upload" : "Choose a file to upload"
            });
            if (!CanTouchWebView(tabKey, webView)) return;

            var script = await BrowserFilePicker.BuildCompletionScriptAsync(request.RequestId, files);
            await webView.InvokeScript(script);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            Trace.TraceWarning($"Could not select upload files: {ex.Message}");
            if (CanTouchWebView(tabKey, webView))
                await webView.InvokeScript(BrowserFilePicker.CancelScript(request.RequestId));
        }
    }

    private void ForceFirstWebKitPaint(string tabKey, NativeWebView webView)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!CanTouchWebView(tabKey, webView) || !webView.IsVisible) return;
            webView.IsVisible = false;
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (CanTouchWebView(tabKey, webView))
                        webView.IsVisible = true;
                },
                DispatcherPriority.Background);
        });
    }

    private async Task NavigatePortWebViewAsync(
        string tabKey,
        string workspaceId,
        NativeWebView webView,
        Uri destination,
        int navigationVersion)
    {
        var errorHtml = await BrowserProbe(destination);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!CanTouchWebView(tabKey, webView)) return;
            if (_navigationVersions.GetValueOrDefault(tabKey) != navigationVersion) return;

            if (errorHtml is null)
            {
                NavigateWebView(webView, destination);
            }
            else
            {
                _lastKnownBrowserUrls.Remove(tabKey);
                NavigateWebView(webView, WriteBrowserErrorPage(workspaceId, errorHtml));
            }
        });
    }

    private static async Task<string?> ProbeBrowserDestinationAsync(Uri destination)
    {
        if (destination.Scheme is not ("http" or "https") || !destination.IsLoopback)
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, destination);
            using var response = await PortProbeHttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            if ((int)response.StatusCode < 500)
                return null;

            return BuildBrowserErrorHtml(
                $"{response.ReasonPhrase ?? "Not found"} {(int)response.StatusCode}",
                response.ReasonPhrase ?? "Request failed",
                destination);
        }
        catch (HttpRequestException ex)
        {
            return BuildBrowserErrorHtml(
                "Could not reach app",
                ex.Message,
                destination);
        }
        catch (TaskCanceledException)
        {
            return BuildBrowserErrorHtml(
                "App did not respond",
                "The request timed out before the embedded browser loaded the page.",
                destination);
        }
    }

    private void ShowBrowserErrorPage(
        string tabKey,
        string workspaceId,
        NativeWebView webView,
        Uri destination,
        string title,
        string detail)
    {
        if (!CanTouchWebView(tabKey, webView)) return;
        _lastKnownBrowserUrls.Remove(tabKey);
        var html = BuildBrowserErrorHtml(title, detail, destination);
        NavigateWebView(webView, WriteBrowserErrorPage(workspaceId, html));
    }

    private static void NavigateWebView(NativeWebView webView, Uri destination)
    {
        if (string.Equals(webView.Source?.ToString(), destination.ToString(), StringComparison.Ordinal))
            return;

        webView.Source = destination;
    }

    internal static bool ShouldReloadConsoleWebView(Uri? currentSource, Uri destination)
        => currentSource is not null
           && string.Equals(currentSource.OriginalString, destination.OriginalString, StringComparison.Ordinal);

    private static void NavigateConsoleWebView(NativeWebView webView, Uri destination)
    {
        // Console output is rewritten to one temp file; a plain navigate would be skipped
        // when the URI is unchanged, leaving the previous application's output visible.
        if (ShouldReloadConsoleWebView(webView.Source, destination))
            ReloadWebView(webView, destination);
        else
            NavigateWebView(webView, destination);
    }

    private static void ReloadWebView(NativeWebView webView, Uri destination)
    {
        webView.Source = new Uri("about:blank");
        Dispatcher.UIThread.Post(() => webView.Source = destination, DispatcherPriority.Background);
    }

    private bool CanTouchWebView(string tabKey, NativeWebView webView)
        => !_isClosed
           && _webViews.TryGetValue(tabKey, out var current)
           && ReferenceEquals(current, webView);

    private bool IsModalOverlayVisible()
        => DataContext is MainViewModel vm && IsModalOverlayVisible(vm);

    private static bool IsModalOverlayVisible(MainViewModel vm)
        => vm.Tutorial.IsVisible
           || vm.Sidebar.DeleteConfirmation.IsVisible
           || vm.Sidebar.AddWorkspace.IsVisible
           || vm.Git.Diff.IsVisible;

    private void ApplyModalOverlayWebViewVisibility(bool modalOverlayVisible)
    {
        if (_isClosed) return;

        foreach (var webView in _webViews.Values)
            webView.IsVisible = false;

        SetConsoleWebViewVisible(!modalOverlayVisible && DataContext is MainViewModel { ShowConsole: true });

        if (modalOverlayVisible)
        {
            WebViewErrorBanner.IsVisible = false;
            return;
        }

        UpdateErrorDisplay(_activeWorkspaceId);
        if (_activeTabKey is null) return;
        if (!_webViews.TryGetValue(_activeTabKey, out var active)) return;
        if (DataContext is not MainViewModel { ShowPortView: true }) return;
        active.IsVisible = true;
    }

    private void SetConsoleWebViewVisible(bool visible)
    {
        if (_consoleWebView is not null)
            _consoleWebView.IsVisible = visible;
        if (_consoleOverlay is not null)
            _consoleOverlay.IsVisible = visible;
    }

    private void HandleBrowserCommand(BrowserCommand command)
    {
        if (_isClosed || _activeWorkspaceId is null) return;
        if (_activeTabKey is not null && _webViews.TryGetValue(_activeTabKey, out var wv))
        {
            switch (command)
            {
                case BrowserCommand.Back: wv.GoBack(); break;
                case BrowserCommand.Forward: wv.GoForward(); break;
                case BrowserCommand.Reload:
                    if (wv.Source is { } src) ReloadWebView(wv, src);
                    break;
            }
        }
    }

    private async Task PollActiveBrowserAddressAsync()
    {
        if (_isClosed) return;
        if (_activeWorkspaceId is null) return;
        if (DataContext is not MainViewModel { ShowPortView: true }) return;

        if (_activeTabKey is null || !_webViews.TryGetValue(_activeTabKey, out var webView)) return;
        var src = webView.Source?.ToString();
        if (string.IsNullOrWhiteSpace(src)) return;

        _lastKnownBrowserUrls[_activeTabKey] = src;
        if (DataContext is MainViewModel vm && !AddressBar.IsFocused)
            vm.UpdateAddressFromBrowser(_activeWorkspaceId, src);
    }

    private void OnAddressPollTimerTick(object? sender, EventArgs e)
        => _ = PollActiveBrowserAddressAsync();

    private void OnConsoleLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isClosed) return;
        if (DataContext is not MainViewModel { ShowConsole: true, Console.IsLoading: false }) return;
        Dispatcher.UIThread.Post(RefreshConsoleWebView);
    }

    private void RefreshConsoleWebView()
    {
        if (_isClosed) return;
        if (DataContext is not MainViewModel vm) return;

        try
        {
            if (_consoleWebView is null)
            {
                var wv = WebViewFactory();
                var firstNavDone = false;
                wv.NavigationCompleted += (_, e) =>
                {
                    if (!e.IsSuccess) return;
                    _ = wv.InvokeScript("var t=document.getElementById('content');if(t)t.scrollTop=t.scrollHeight;");
                    if (firstNavDone) return;
                    firstNavDone = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (!ReferenceEquals(_consoleWebView, wv) || !wv.IsVisible) return;
                        wv.IsVisible = false;
                        Dispatcher.UIThread.Post(
                            () =>
                            {
                                if (ReferenceEquals(_consoleWebView, wv) && !IsModalOverlayVisible())
                                    wv.IsVisible = true;
                            },
                            DispatcherPriority.Background);
                    });
                };
                ConsolePane.Children.Add(wv);
                _consoleWebView = wv;

                var overlay = new Panel
                {
                    Background = Brushes.Transparent,
                    Cursor = new Cursor(StandardCursorType.Ibeam),
                    Focusable = true,
                };
                overlay.PointerPressed += OnConsoleOverlayPointerPressed;
                overlay.PointerMoved += OnConsoleOverlayPointerMoved;
                overlay.PointerReleased += OnConsoleOverlayPointerReleased;
                overlay.PointerWheelChanged += OnConsoleOverlayWheelChanged;
                overlay.KeyDown += OnConsoleOverlayKeyDown;
                ConsolePane.Children.Add(overlay);
                _consoleOverlay = overlay;
            }

            IEnumerable<string> linesToShow = vm.Console.ShowAllLines
                ? vm.Console.Lines
                : vm.Console.Lines.Skip(Math.Max(0, vm.Console.Lines.Count - ConsoleDefaultDisplayLines));

            var html = BuildConsoleHtml(linesToShow);
            var htmlPath = ConsoleHtmlPath();
            File.WriteAllText(htmlPath, html, Encoding.UTF8);
            NavigateConsoleWebView(_consoleWebView, new Uri("file://" + htmlPath));
            SetConsoleWebViewVisible(!IsModalOverlayVisible());
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Trace.TraceWarning($"Could not load console WebView: {ex.Message}");
            if (_consoleOverlay is not null)
            {
                try { ConsolePane.Children.Remove(_consoleOverlay); } catch (InvalidOperationException rex) { Trace.TraceWarning(rex.Message); }
                _consoleOverlay = null;
            }
            _consoleWebView = null;
        }
    }

    private void DestroyConsoleWebView()
    {
        if (_consoleOverlay is not null)
        {
            try { ConsolePane.Children.Remove(_consoleOverlay); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
            _consoleOverlay = null;
        }
        if (_consoleWebView is null) return;
        try { _consoleWebView.Stop(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
        try { ConsolePane.Children.Remove(_consoleWebView); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
        if (_consoleWebView is IDisposable disposable)
            try { disposable.Dispose(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
        _consoleWebView = null;
        try { File.Delete(ConsoleHtmlPath()); } catch (IOException ex) { Trace.TraceWarning(ex.Message); }
    }

    private static readonly string _consoleHtmlPath =
        Path.Join(Path.GetTempPath(), $"agentup-console-{Environment.ProcessId}.html");

    private static string ConsoleHtmlPath() => _consoleHtmlPath;

    private static string BrowserErrorHtmlPath(string workspaceId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workspaceId)));
        return Path.Join(Path.GetTempPath(), $"agentup-browser-error-{hash[..16]}.html");
    }

    private static Uri WriteBrowserErrorPage(string workspaceId, string html)
    {
        var htmlPath = BrowserErrorHtmlPath(workspaceId);
        File.WriteAllText(htmlPath, html, Encoding.UTF8);
        return new Uri("file://" + htmlPath);
    }

    internal static string BuildBrowserErrorHtml(string title, string detail, Uri destination)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeDetail = WebUtility.HtmlEncode(detail);
        var safeUrl = WebUtility.HtmlEncode(destination.ToString());

        return $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<style>
* { box-sizing: border-box; }
html, body {
  min-height: 100%;
  margin: 0;
  background: #000000;
  color: #f5fbf7;
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}
body {
  display: grid;
  place-items: center;
  padding: 32px;
}
.panel {
  width: min(620px, 100%);
  border: 1px solid #287038;
  border-radius: 8px;
  background: #050505;
  box-shadow: 0 0 34px rgba(0, 184, 80, 0.18);
  padding: 28px;
}
h1 {
  margin: 0 0 10px;
  color: #f5fbf7;
  font-size: 30px;
  line-height: 1.1;
}
.detail {
  display: block;
  margin: 0 0 18px;
  color: #b0c8b8;
  font-size: 14px;
}
code {
  display: block;
  padding: 12px;
  border: 1px solid #184820;
  border-radius: 7px;
  background: #000000;
  color: #00d66b;
  font-family: Consolas, "Courier New", monospace;
  font-size: 12px;
  overflow-wrap: anywhere;
}
</style>
</head>
<body>
  <main class="panel">
    <h1>{{safeTitle}}</h1>
    <span class="detail">{{safeDetail}}</span>
    <code>{{safeUrl}}</code>
  </main>
</body>
</html>
""";
    }

    internal static string BuildConsoleHtml(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>");
        sb.Append("* { margin: 0; padding: 0; box-sizing: border-box; }");
        sb.Append("html, body { height: 100%; overflow: hidden; background: #000000; }");
        sb.Append("::selection { background-color: #0f7a45; color: #f5fbf7; }");
        sb.Append("#content { display: block; width: 100%; height: 100%; background: #000000; color: #c7d9d0; font-family: Consolas,'Courier New',monospace; font-size: 12px; padding: 14px 20px; white-space: pre; overflow: auto; line-height: 1.4; outline: none; cursor: text; }");
        sb.Append("</style></head><body>");
        sb.Append("<pre id=\"content\" tabindex=\"-1\">");
        foreach (var line in lines)
        {
            AppendHtmlLine(sb, line);
            sb.Append('\n');
        }
        sb.Append("</pre>");
        sb.Append("<script>");
        sb.Append(ConsoleJs);
        sb.Append("</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void AppendHtmlLine(StringBuilder sb, string line)
    {
        var i = 0;
        while (i < line.Length)
        {
            var c = line[i];
            if (c == '\x1B' && i + 1 < line.Length && line[i + 1] == '[')
            {
                i += 2;
                while (i < line.Length && !char.IsLetter(line[i]))
                    i++;
                if (i < line.Length) i++;
            }
            else
            {
                sb.Append(c switch { '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", _ => null });
                if (c is not '&' and not '<' and not '>')
                    sb.Append(c);
                i++;
            }
        }
    }

    private void DestroyWorkspaceWebViews()
    {
        foreach (var tabKey in _webViews.Keys.ToList())
            DestroyWorkspaceWebView(tabKey);

        _webViewErrors.Clear();
        _lastKnownBrowserUrls.Clear();
        _navigationVersions.Clear();
        _activeWorkspaceId = null;
        _activeTabKey = null;
    }

    private void DestroyWorkspaceWebViews(string workspaceId)
    {
        foreach (var tabKey in _webViews.Keys.Where(key => key.StartsWith($"{workspaceId}:", StringComparison.Ordinal)).ToList())
            DestroyWorkspaceWebView(tabKey);

        _webViewErrors.Remove(workspaceId);
        DeleteBrowserErrorPage(workspaceId);

        if (_activeWorkspaceId != workspaceId)
            return;

        _activeWorkspaceId = null;
        _activeTabKey = null;
        UpdateErrorDisplay(null);
    }

    private void DestroyWorkspaceWebView(string tabKey)
    {
        if (!_webViews.Remove(tabKey, out var webView))
            return;

        try { webView.Stop(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
        try { PortPane.Children.Remove(webView); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
        if (webView is IDisposable disposable)
            try { disposable.Dispose(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }

        _lastKnownBrowserUrls.Remove(tabKey);
        _navigationVersions.Remove(tabKey);
        ClosePopups(tabKey);
    }

    private static void DeleteBrowserErrorPage(string workspaceId)
    {
        try { File.Delete(BrowserErrorHtmlPath(workspaceId)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Trace.TraceWarning(ex.Message); }
    }

    internal static string? NormalizeScriptResult(string? scriptResult)
    {
        if (string.IsNullOrWhiteSpace(scriptResult)) return scriptResult;

        var candidate = scriptResult.Trim();
        if (candidate.Length < 2 || candidate[0] != '"' || candidate[^1] != '"')
            return scriptResult;

        try
        {
            return JsonSerializer.Deserialize<string>(candidate);
        }
        catch (JsonException)
        {
            return candidate[1..^1].Replace("\\/", "/").Replace("\\\"", "\"");
        }
    }

    private void WakeActiveWebView()
    {
        if (_isClosed || _activeTabKey is null || _activeWorkspaceId is null) return;
        if (!_webViews.TryGetValue(_activeTabKey, out var webView)) return;

        if (_lastKnownBrowserUrls.TryGetValue(_activeTabKey, out var lastUrl)
            && Uri.TryCreate(lastUrl, UriKind.Absolute, out var lastUri)
            && !string.Equals(webView.Source?.ToString(), lastUrl, StringComparison.Ordinal))
        {
            var ver = _navigationVersions.GetValueOrDefault(_activeTabKey) + 1;
            _navigationVersions[_activeTabKey] = ver;
            _ = NavigatePortWebViewAsync(_activeTabKey, _activeWorkspaceId, webView, lastUri, ver);
        }
    }

    private void OnPortPaneSizeChanged(object? sender, SizeChangedEventArgs e) { }

    private static readonly JsonSerializerOptions _auditJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private void RecordWebViewEvent(string workspaceId, string action, string outcome, Dictionary<string, string> details)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var dto = new { kind = "desktop", source = "webview", action, outcome, workspaceId, details };
                var json = JsonSerializer.Serialize(dto, _auditJsonOptions);
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                await _serverHttp.PostAsync("/api/audit/record", content);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ObjectDisposedException)
            {
                Trace.TraceWarning(ex.Message);
            }
        });
    }

    private void OnConsoleOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_consoleWebView is null || _isClosed) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _consoleOverlay?.Focus();
        var pos = e.GetPosition(_consoleOverlay);
        if (e.ClickCount >= 3)
        {
            _consoleSelecting = false;
            _ = _consoleWebView.InvokeScript($"window._selLine({pos.X:F1},{pos.Y:F1})");
        }
        else if (e.ClickCount == 2)
        {
            _consoleSelecting = false;
            _ = _consoleWebView.InvokeScript($"window._selWord({pos.X:F1},{pos.Y:F1})");
        }
        else
        {
            _consoleSelecting = true;
            _ = _consoleWebView.InvokeScript($"window._selStart({pos.X:F1},{pos.Y:F1})");
        }
    }

    private void OnConsoleOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_consoleSelecting || _consoleWebView is null || _isClosed) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _consoleSelecting = false;
            return;
        }
        var pos = e.GetPosition(_consoleOverlay);
        _ = _consoleWebView.InvokeScript($"window._selExtend({pos.X:F1},{pos.Y:F1})");
    }

    private void OnConsoleOverlayPointerReleased(object? sender, PointerReleasedEventArgs e)
        => _consoleSelecting = false;

    private void OnConsoleOverlayWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_consoleWebView is null || _isClosed) return;
        var delta = -e.Delta.Y * 60.0;
        _ = _consoleWebView.InvokeScript($"window._scroll({delta:F1})");
    }

    private async void OnConsoleOverlayKeyDown(object? sender, KeyEventArgs e)
    {
        if (_consoleWebView is null || _isClosed) return;
        if (DataContext is not MainViewModel vm) return;

        var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        if (ConsoleKeyboardInputProvider.IsInterruptKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            try
            {
                var result = await _consoleWebView.InvokeScript(
                    "(function(){var s=window.getSelection();return s?s.toString():'';})()");
                var text = NormalizeScriptResult(result);
                if (!string.IsNullOrEmpty(text))
                {
                    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clipboard is not null)
                        await clipboard.SetTextAsync(text);
                    return;
                }

                var workspaceId = vm.Sidebar.SelectedWorkspace?.Id;
                var application = vm.Applications.SelectedApplication?.Name;
                if (workspaceId is null || application is null) return;

                using var response = await _serverHttp.PostAsync(
                    $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(application)}/stop",
                    null);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException or HttpRequestException)
            {
                Trace.TraceWarning(ex.Message);
            }

            return;
        }

        if (!ConsoleKeyboardInputProvider.IsCopyKey(e.Key, e.KeyModifiers, isMac)) return;

        e.Handled = true;
        try
        {
            var result = await _consoleWebView.InvokeScript(
                "(function(){var s=window.getSelection();return s?s.toString():'';})()");
            var text = NormalizeScriptResult(result);
            if (!string.IsNullOrEmpty(text))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is not null)
                    await clipboard.SetTextAsync(text);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            Trace.TraceWarning(ex.Message);
        }
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
           || IsNamedChromeControl(source, "SidebarToggle")
           || IsNamedChromeControl(source, "ReloadButton");

    private static bool IsNamedChromeControl(Visual source, string name)
    {
        for (var current = source; current is not null; current = current.GetVisualParent() as Visual)
        {
            if (current is Control { Name: var controlName }
                && string.Equals(controlName, name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void OnCloseWindowClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnMinimizeWindowClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnRestoreWindowClicked(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void ToggleMaximized()
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnOpenTutorialFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { Tutorial.ProjectDirectory: { Length: > 0 } path }) return;
        if (!Directory.Exists(path)) return;

        var (fileName, arguments) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("explorer.exe", $"\"{path}\"")
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? ("open", $"\"{path}\"")
                : ("xdg-open", $"\"{path}\"");

        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }
}

// Thin seam over NativeWebDialog so tests can substitute a fake popup without ever
// constructing a real native web dialog (doing so requires a working GTK/WebKit
// environment and crashes the test process where one isn't available, e.g. plain `dotnet test`
// on Linux without Xvfb).
internal interface IWebPopup : IDisposable
{
    string Title { set; }
    event EventHandler Closing;
    void Navigate(Uri uri);
    void Show();
}

internal sealed class NativeWebDialogPopup : IWebPopup
{
    private readonly NativeWebDialog _dialog = new();

    public string Title { set => _dialog.Title = value; }

    public event EventHandler? Closing
    {
        add => _dialog.Closing += value;
        remove => _dialog.Closing -= value;
    }

    public void Navigate(Uri uri) => _dialog.Navigate(uri);

    public void Show() => _dialog.Show();

    public void Dispose() => _dialog.Dispose();
}
