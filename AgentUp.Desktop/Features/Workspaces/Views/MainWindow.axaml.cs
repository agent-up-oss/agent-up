using System.Diagnostics;
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
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Repositories;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    private readonly Dictionary<string, NativeWebView> _webViews = new();
    private readonly Dictionary<string, string> _webViewErrors = new();
    private readonly Dictionary<string, string> _lastKnownBrowserUrls = new();
    private readonly CompositeDisposable _subscriptions = new();
    private readonly DispatcherTimer _addressPollTimer;
    private string? _activeWorkspaceId;
    private bool _isClosed;
    private NativeWebView? _consoleWebView;
    private Panel? _consoleOverlay;
    private bool _consoleSelecting;

    // Overrideable in tests to inject a factory that throws without needing GTK.
    internal Func<NativeWebView> WebViewFactory { get; set; } = () => new NativeWebView();
    internal bool HasBrowserResourcesForTests =>
        _addressPollTimer.IsEnabled
        || _webViews.Count > 0
        || _webViewErrors.Count > 0
        || _lastKnownBrowserUrls.Count > 0
        || _activeWorkspaceId is not null;

    // Injected into port WebViews after each navigation. Injects ::selection CSS so the
    // highlight is visible even when the GTK widget isn't keyboard-focused (inactive
    // selection uses a grey that's invisible on many backgrounds). Also wires _selStart/
    // _selEnd helpers for platforms where Avalonia can relay pointer events.
    private const string SelectionJs =
        "(function(){" +
        "if(!document.getElementById('_au_sel')){" +
        "var st=document.createElement('style');st.id='_au_sel';" +
        "st.textContent='::selection{background-color:#17614f!important;color:#f5fbf7!important}';" +
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

    // Embedded in the console HTML page. The Avalonia overlay above the WebView drives all
    // interactions via InvokeScript — no mousemove listener needed since Avalonia reports moves.
    // _selStart / _selWord / _selLine also focus the <pre> element so WebKit renders the
    // selection with the active (coloured) highlight rather than the invisible inactive grey.
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

    public MainWindow()
    {
        InitializeComponent();
        SetWindowIcon();
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        _addressPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _addressPollTimer.Tick += OnAddressPollTimerTick;
        _addressPollTimer.Start();
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
            // Window icons are best-effort and should never block Desktop startup.
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
        base.OnDataContextChanged(e);
        if (DataContext is not MainViewModel vm) return;
        _subscriptions.Clear();
        vm.BrowserNavigation.Subscribe(nav =>
            Dispatcher.UIThread.Post(() => HandleNavigation(nav.WorkspaceId, nav.Url)))
            .DisposeWith(_subscriptions);
        vm.BrowserCommands.Subscribe(command =>
            Dispatcher.UIThread.Post(() => HandleBrowserCommand(command)))
            .DisposeWith(_subscriptions);
        vm.Tutorial.WhenAnyValue(t => t.IsVisible)
            .DistinctUntilChanged()
            .Subscribe(isVisible =>
                Dispatcher.UIThread.Post(() => ApplyTutorialWebViewVisibility(isVisible)))
            .DisposeWith(_subscriptions);
        ApplyTutorialWebViewVisibility(vm.Tutorial.IsVisible);
        // Refresh when loading completes while console tab is visible.
        vm.Console.WhenAnyValue(c => c.IsLoading)
            .Where(loading => !loading)
            .Skip(1)
            .Where(_ => vm.ShowConsole)
            .Subscribe(_ => Dispatcher.UIThread.Post(RefreshConsoleWebView))
            .DisposeWith(_subscriptions);
        // Refresh when user switches to the console tab (data may already be loaded).
        vm.WhenAnyValue(v => v.ShowConsole)
            .Where(visible => visible && !vm.Console.IsLoading)
            .Subscribe(_ => Dispatcher.UIThread.Post(RefreshConsoleWebView))
            .DisposeWith(_subscriptions);
        // Refresh when the user expands to show all lines.
        vm.Console.WhenAnyValue(c => c.ShowAllLines)
            .Where(all => all && vm.ShowConsole)
            .Subscribe(_ => Dispatcher.UIThread.Post(RefreshConsoleWebView))
            .DisposeWith(_subscriptions);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _addressPollTimer.Stop();
        _addressPollTimer.Tick -= OnAddressPollTimerTick;
        _subscriptions.Dispose();
        DestroyWorkspaceWebViews();
        DestroyConsoleWebView();
        base.OnClosed(e);
    }

    internal void NavigateTo(string workspaceId, string? url) => HandleNavigation(workspaceId, url);

    // Evaluates a JavaScript expression in the WebView for the given workspace and returns
    // the result as a string. Returns null if no WebView exists for that workspace yet.
    internal async Task<string?> EvalAsync(string workspaceId, string script)
    {
        if (!_webViews.TryGetValue(workspaceId, out var webView)) return null;
        // InvokeScript is synchronous but touches UI state — run it on the UI thread.
        var result = await Dispatcher.UIThread.InvokeAsync(() => webView.InvokeScript(script));
        return NormalizeScriptResult(result);
    }

    private void UpdateErrorDisplay(string? workspaceId)
    {
        if (IsTutorialVisible())
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

    private void HandleNavigation(string? workspaceId, string? url)
    {
        if (_isClosed) return;

        var tutorialVisible = IsTutorialVisible();
        ActivateWorkspaceWebView(workspaceId, tutorialVisible);

        if (url is null || workspaceId is null) return;
        if (!TryGetOrCreateWebView(workspaceId, url, out var webView, out var destinationUrl)) return;

        webView.IsVisible = !tutorialVisible;
        NavigateWebView(webView, new Uri(destinationUrl));
    }

    private void ActivateWorkspaceWebView(string? workspaceId, bool tutorialVisible)
    {
        if (workspaceId == _activeWorkspaceId) return;

        if (_activeWorkspaceId is not null && _webViews.TryGetValue(_activeWorkspaceId, out var previous))
            previous.IsVisible = false;

        _activeWorkspaceId = workspaceId;

        if (workspaceId is not null && _webViews.TryGetValue(workspaceId, out var existing))
            existing.IsVisible = !tutorialVisible;

        UpdateErrorDisplay(workspaceId);
    }

    private bool TryGetOrCreateWebView(
        string workspaceId,
        string requestedUrl,
        out NativeWebView webView,
        out string destinationUrl)
    {
        destinationUrl = requestedUrl;
        if (_webViews.TryGetValue(workspaceId, out webView!))
            return true;

        try
        {
            webView = CreateWorkspaceWebView(workspaceId);
            _webViews[workspaceId] = webView;
            _webViewErrors.Remove(workspaceId);

            // Restore last-visited URL before the first navigation.
            destinationUrl = BrowserUrlStore.Read(workspaceId, requestedUrl) ?? requestedUrl;

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

    private NativeWebView CreateWorkspaceWebView(string workspaceId)
    {
        var webView = WebViewFactory();
        webView.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(NativeWebView.Source))
                UpdateAddressFromWebView(workspaceId, webView.Source);
        };
        webView.EnvironmentRequested += (_, e) => ConfigureWebViewProfile(workspaceId, e);

        var firstNavDone = false;
        webView.NavigationCompleted += (_, e) =>
        {
            if (!e.IsSuccess || e.Request is not { } uri) return;
            UpdateAddressFromWebView(workspaceId, uri);
            _ = webView.InvokeScript(SelectionJs);
            if (firstNavDone) return;
            firstNavDone = true;
            ForceFirstWebKitPaint(workspaceId, webView);
        };

        return webView;
    }

    private static void NavigateWebView(NativeWebView webView, Uri destination)
    {
        if (string.Equals(webView.Source?.ToString(), destination.ToString(), StringComparison.Ordinal))
        {
            webView.Source = new Uri("about:blank");
            Dispatcher.UIThread.Post(() => webView.Source = destination, DispatcherPriority.Background);
            return;
        }

        webView.Source = destination;
    }

    private void ForceFirstWebKitPaint(string workspaceId, NativeWebView webView)
    {
        // WebKit renders content into the native window but GTK only composites it when
        // the embedded window receives an Expose event. A hide/show at separate dispatcher
        // priorities forces the repaint after first navigation.
        Dispatcher.UIThread.Post(() =>
        {
            if (!CanTouchWebView(workspaceId, webView) || !webView.IsVisible) return;
            webView.IsVisible = false;
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (CanTouchWebView(workspaceId, webView))
                        webView.IsVisible = true;
                },
                DispatcherPriority.Background);
        });
    }

    private bool CanTouchWebView(string workspaceId, NativeWebView webView)
        => !_isClosed
           && _webViews.TryGetValue(workspaceId, out var current)
           && ReferenceEquals(current, webView);

    private bool IsTutorialVisible()
        => DataContext is MainViewModel { Tutorial.IsVisible: true };

    private void ApplyTutorialWebViewVisibility(bool tutorialVisible)
    {
        if (_isClosed) return;

        foreach (var webView in _webViews.Values)
            webView.IsVisible = false;

        if (tutorialVisible)
        {
            WebViewErrorBanner.IsVisible = false;
            return;
        }
        UpdateErrorDisplay(_activeWorkspaceId);
        if (_activeWorkspaceId is null) return;
        if (!_webViews.TryGetValue(_activeWorkspaceId, out var active)) return;
        if (DataContext is not MainViewModel { ShowPortView: true }) return;

        active.IsVisible = true;
    }

    private void HandleBrowserCommand(BrowserCommand command)
    {
        if (_isClosed) return;
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
        if (_isClosed) return;
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
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            // The page may still be loading or the native WebView may not be ready yet.
            Trace.TraceWarning(ex.Message);
        }
    }

    private void OnAddressPollTimerTick(object? sender, EventArgs e)
        => _ = PollActiveBrowserAddressAsync();

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
                    // Same Expose-event fix as ForceFirstWebKitPaint: GTK only composites the
                    // native WebKit window when XMapWindow fires an Expose event. Because the
                    // window was already mapped before navigation completed, no Expose arrives.
                    // A hide→show at separate dispatcher priorities forces the repaint.
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (!ReferenceEquals(_consoleWebView, wv) || !wv.IsVisible) return;
                        wv.IsVisible = false;
                        Dispatcher.UIThread.Post(
                            () => { if (ReferenceEquals(_consoleWebView, wv)) wv.IsVisible = true; },
                            DispatcherPriority.Background);
                    });
                };
                ConsolePane.Children.Add(wv);
                _consoleWebView = wv;

                // Transparent overlay stacked above the WebView so Avalonia sees pointer events.
                // On GTK/X11, button-press events go directly to WebKit's native window; they
                // never reach Avalonia's event system. An ordinary Avalonia control does receive
                // them, so the overlay intercepts press/move/release and drives selection via
                // InvokeScript. Scroll wheel is also forwarded so the overlay doesn't break it.
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
                : vm.Console.Lines.Skip(Math.Max(0, vm.Console.Lines.Count - ConsoleViewModel.DefaultDisplayLines));

            var html = BuildConsoleHtml(linesToShow);
            var htmlPath = ConsoleHtmlPath();
            File.WriteAllText(htmlPath, html, Encoding.UTF8);
            NavigateWebView(_consoleWebView, new Uri("file://" + htmlPath));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // WebView creation is best-effort — GTK/WebView2 may not be available in all environments.
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

    private static string ConsoleHtmlPath()
        => Path.Join(Path.GetTempPath(), "agentup-console-output.html");

    internal static string BuildConsoleHtml(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>");
        sb.Append("* { margin: 0; padding: 0; box-sizing: border-box; }");
        sb.Append("html, body { height: 100%; overflow: hidden; background: #07110f; }");
        sb.Append("::selection { background-color: #17614f; color: #f5fbf7; }");
        sb.Append("#content { display: block; width: 100%; height: 100%; background: #07110f; color: #c6d4ce; font-family: Consolas,'Courier New',monospace; font-size: 12px; padding: 14px 20px; white-space: pre; overflow: auto; line-height: 1.4; outline: none; cursor: text; }");
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
                // Skip ESC[ ... <letter> (CSI escape sequence)
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
        foreach (var webView in _webViews.Values)
        {
            try { webView.Stop(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
            try { PortPane.Children.Remove(webView); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
            if (webView is IDisposable disposable)
            {
                try { disposable.Dispose(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
            }
        }

        _webViews.Clear();
        _webViewErrors.Clear();
        _lastKnownBrowserUrls.Clear();
        _activeWorkspaceId = null;
    }

    internal static string? TryReadHttpLocation(string? scriptResult)
    {
        scriptResult = NormalizeScriptResult(scriptResult);
        if (string.IsNullOrWhiteSpace(scriptResult)) return null;

        var candidate = scriptResult.Trim();

        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
               && uri.Scheme is "http" or "https"
            ? uri.ToString()
            : null;
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

    internal static void ConfigureWebViewProfile(string workspaceId, WebViewEnvironmentRequestedEventArgs e)
    {
        var profileRoot = BrowserUrlStore.ProfilePath(workspaceId);

        switch (e)
        {
            case GtkWebViewEnvironmentRequestedEventArgs gtk:
                gtk.BaseDataDirectory = Path.Join(profileRoot, "data");
                gtk.BaseCacheDirectory = Path.Join(profileRoot, "cache");
                break;
            case LinuxWpeWebViewEnvironmentRequestedEventArgs wpe:
                wpe.DataDirectory = Path.Join(profileRoot, "data");
                wpe.CacheDirectory = Path.Join(profileRoot, "cache");
                break;
            case WindowsWebView2EnvironmentRequestedEventArgs webView2:
                webView2.UserDataFolder = Path.Join(profileRoot, "webview2");
                webView2.ProfileName = SafeProfileName(workspaceId);
                break;
            case AppleWKWebViewEnvironmentRequestedEventArgs apple:
                apple.DataStoreIdentifier = StableGuid(workspaceId);
                break;
        }
    }

    private static string SafeProfileName(string workspaceId)
    {
        var builder = new StringBuilder(workspaceId.Length);
        foreach (var c in workspaceId)
            builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');

        return builder.Length > 0 ? builder.ToString() : "workspace";
    }

    private static Guid StableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash[..16]);
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
        if (e.Key != Key.C || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (_consoleWebView is null || _isClosed) return;
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
           || SidebarToggle.IsVisualAncestorOf(source)
           || ReloadButton.IsVisualAncestorOf(source);

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
            // Opening the file manager is a convenience action; setup checks remain authoritative.
            Trace.TraceWarning(ex.Message);
        }
    }
}
