using System.Reflection;
using Avalonia.Controls;

namespace AgentUp.Tests.Support;

// NativeWebView.NewWindowRequested is a genuine native-engine callback with no public way to
// raise it, and the gesture that triggers it — a real mouse click inside the native WebView —
// cannot be injected from a test runner on any of the three platforms. Tests therefore invoke
// the event's backing delegate directly, exactly as WebKitGTK, WKWebView, or WebView2 does when
// a page calls window.open(); everything Desktop does in response stays real.
internal static class NativeWebViewEvents
{
    internal static WebViewNewWindowRequestedEventArgs RaiseNewWindowRequested(NativeWebView webView, Uri request)
    {
        var args = new WebViewNewWindowRequestedEventArgs { Request = request };
        var field = typeof(NativeWebView).GetField("_newWindowRequested", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "NativeWebView._newWindowRequested field not found — the Avalonia.Controls.WebView package layout changed.");

        var handler = (EventHandler<WebViewNewWindowRequestedEventArgs>?)field.GetValue(webView);
        Assert.That(handler, Is.Not.Null, "Desktop must subscribe to NewWindowRequested so sign-in popups have somewhere to go.");
        handler!.Invoke(webView, args);
        return args;
    }
}
