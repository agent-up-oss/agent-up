using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AgentUp.Tests.Support;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AgentUp.Tests.Features.Browser.E2E;

// End-to-end coverage for https://github.com/agent-up-oss/agent-up/issues/270 — HTML file
// inputs did nothing when a page was rendered inside a Desktop workspace WebView, so users
// could not upload anything from an application running inside Agent-Up.
//
// These tests run the real MainWindow against the platform WebView (WebKitGTK on Linux,
// WKWebView on macOS, WebView2 on Windows) and the platform storage provider (the XDG desktop
// portal or the GTK chooser on Linux, AppKit on macOS, the Win32 common dialog on Windows).
// Only the chooser dialog itself is substituted, through MainWindow.FilePicker: no test runner
// can drive a native modal file chooser, but everything on both sides of it is real — the
// injected bridge script, the WebView message, the Avalonia IStorageFile read, the completion
// script, the reconstructed DOM FileList, and an actual HTTP upload of the picked bytes.
//
// Run: dotnet test AgentUp.Tests/ --filter "Category=E2E"
[TestFixture, Category("E2E")]
public sealed class DesktopWebViewFilePickerTests
{
    // A space exercises name escaping through JSON, the DOM, and the upload query string.
    // Non-ASCII names are covered by the provider unit tests instead: macOS normalises file
    // names on disk (NFD), so a literal comparison against an NFC constant is not a portable
    // assertion about this bridge.
    private const string NoteFileName = "upload note.txt";
    private const string DiagramFileName = "diagram.svg";
    private const string NoteContent = "agent-up upload round trip\nsecond line";
    private const string DiagramContent = "<svg xmlns=\"http://www.w3.org/2000/svg\"><title>e2e</title></svg>";

    private const string PageHtml = """
        <!DOCTYPE html>
        <html><body>
          <h1 id="app">Upload demo</h1>
          <input id="single" type="file">
          <input id="many" type="file" multiple>
          <script>
            window.__nav = '__AGENTUP_NAV__';
            window.__events = [];
            window.__text = '';
            window.__uploaded = 'idle';
            ['single', 'many'].forEach(function (id) {
              var input = document.getElementById(id);
              input.addEventListener('input', function () { window.__events.push(id + ':input'); });
              input.addEventListener('change', function () { window.__events.push(id + ':change'); });
            });
            window.__describe = function (id) {
              var files = document.getElementById(id).files;
              return 'files:' + Array.prototype.map.call(files, function (file) {
                return file.name + '|' + file.type + '|' + file.size;
              }).join(',');
            };
            window.__eventLog = function () { return 'events:' + window.__events.join(','); };
            window.__readText = function (id) {
              var file = document.getElementById(id).files[0];
              window.__text = '';
              var reader = new FileReader();
              reader.onload = function () { window.__text = reader.result; };
              reader.readAsText(file);
              return 'reading';
            };
            window.__upload = function (id) {
              var file = document.getElementById(id).files[0];
              window.__uploaded = 'pending';
              fetch('/upload?name=' + encodeURIComponent(file.name), {
                method: 'POST',
                body: file,
                headers: { 'Content-Type': file.type || 'application/octet-stream' }
              }).then(function () { window.__uploaded = 'done'; });
              return 'uploading';
            };
            // Sends exactly the message Desktop's injected bridge sends for a trusted click on
            // a file input. A synthetic click cannot stand in for that: the bridge deliberately
            // ignores untrusted events, which is asserted separately below.
            window.__pick = function (id, requestId, multiple) {
              window.__agentUpFilePickerRequests.set(requestId, document.getElementById(id));
              window.invokeCSharpAction(JSON.stringify({
                type: 'agent-up:file-picker', requestId: requestId, multiple: multiple
              }));
              return 'sent';
            };
            window.__pending = function () { return String(window.__agentUpFilePickerRequests.size); };
          </script>
        </body></html>
        """;

    private string _fileRoot = null!;
    private string _notePath = null!;
    private string _diagramPath = null!;
    private HtmlAppServer _server = null!;
    private DesktopBrowserHarness _desktop = null!;

    [OneTimeSetUp]
    public async Task StartDesktop()
    {
        _fileRoot = Path.Join(Path.GetTempPath(), $"agentup-e2e-upload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_fileRoot);
        _notePath = Path.Join(_fileRoot, NoteFileName);
        _diagramPath = Path.Join(_fileRoot, DiagramFileName);
        await File.WriteAllTextAsync(_notePath, NoteContent, new UTF8Encoding(false));
        await File.WriteAllTextAsync(_diagramPath, DiagramContent, new UTF8Encoding(false));

        _server = new HtmlAppServer(PageHtml);
        _desktop = await DesktopBrowserHarness.LaunchAsync(_server.Port);
    }

    [OneTimeTearDown]
    public async Task StopDesktop()
    {
        await _desktop.DisposeAsync();
        _server.Dispose();

        try
        {
            Directory.Delete(_fileRoot, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }

    // Every test starts from a freshly loaded document so page state and the injected bridge
    // are rebuilt exactly as they are after any real navigation.
    [SetUp]
    public async Task LoadFreshPage()
    {
        await _desktop.NavigateAsync(_server.BaseUrl);
        await _desktop.WaitForFilePickerBridgeAsync();
        await _desktop.WaitForScriptAsync("window.__pending()", "0", "The page did not start with an empty request map");
    }

    [Test, CancelAfter(60000), Timeout(60000)]
    public async Task FilePicker_singleSelection_reachesThePageAndUploadsTheRealFileBytes()
    {
        var picked = await ResolvePlatformFilesAsync(_notePath);
        var requests = await CaptureFilePickerAsync(picked);

        Assert.That(await _desktop.EvalAsync("window.__pick('single','req-single',false)"), Is.EqualTo("sent"));

        var noteBytes = Encoding.UTF8.GetByteCount(NoteContent);
        await _desktop.WaitForScriptAsync(
            "window.__describe('single')",
            $"files:{NoteFileName}|text/plain|{noteBytes}",
            "The picked file never arrived in the page's file input");

        await _desktop.EvalAsync("window.__readText('single')");
        await _desktop.WaitForScriptAsync(
            "window.__text",
            NoteContent,
            "The page could not read back the bytes of the picked file");

        await _desktop.EvalAsync("window.__upload('single')");
        await _desktop.WaitForScriptAsync("window.__uploaded", "done", "The page never finished uploading the picked file");
        var upload = await _server.WaitForUploadAsync();
        var events = await _desktop.EvalAsync("window.__eventLog()");
        var pending = await _desktop.EvalAsync("window.__pending()");

        Assert.Multiple(() =>
        {
            Assert.That(requests, Has.Count.EqualTo(1), "Desktop must open the native picker exactly once per request");
            Assert.That(requests[0].AllowMultiple, Is.False, "A single-file input must not ask for a multi-selection");
            Assert.That(requests[0].Title, Is.EqualTo("Choose a file to upload"));
            Assert.That(upload.Name, Is.EqualTo(NoteFileName), "The uploaded file must keep its original name");
            Assert.That(Encoding.UTF8.GetString(upload.Content), Is.EqualTo(NoteContent));
            Assert.That(upload.ContentType, Does.StartWith("text/plain"));
            Assert.That(events, Is.EqualTo("events:single:input,single:change"),
                "The page must observe the standard input and change events a real selection raises");
            Assert.That(pending, Is.EqualTo("0"), "A completed request must not stay pending in the page");
        });
    }

    [Test, CancelAfter(60000), Timeout(60000)]
    public async Task FilePicker_multipleSelection_deliversEveryFileInOrder()
    {
        var picked = await ResolvePlatformFilesAsync(_notePath, _diagramPath);
        var requests = await CaptureFilePickerAsync(picked);

        Assert.That(await _desktop.EvalAsync("window.__pick('many','req-many',true)"), Is.EqualTo("sent"));

        var noteBytes = Encoding.UTF8.GetByteCount(NoteContent);
        var diagramBytes = Encoding.UTF8.GetByteCount(DiagramContent);
        await _desktop.WaitForScriptAsync(
            "window.__describe('many')",
            $"files:{NoteFileName}|text/plain|{noteBytes},{DiagramFileName}|image/svg+xml|{diagramBytes}",
            "A multi-file selection must reach the page complete and in the order it was picked");

        Assert.Multiple(() =>
        {
            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].AllowMultiple, Is.True, "An input marked multiple must ask for a multi-selection");
            Assert.That(requests[0].Title, Is.EqualTo("Choose files to upload"));
        });
    }

    [Test, CancelAfter(60000), Timeout(60000)]
    public async Task FilePicker_cancelledSelection_leavesTheInputEmptyAndDropsTheRequest()
    {
        await CaptureFilePickerAsync([]);

        Assert.That(await _desktop.EvalAsync("window.__pick('single','req-cancelled',false)"), Is.EqualTo("sent"));
        await _desktop.WaitForScriptAsync(
            "window.__pending()",
            "0",
            "A cancelled picker must clear the pending request so the page stops waiting on it");

        var selection = await _desktop.EvalAsync("window.__describe('single')");
        var events = await _desktop.EvalAsync("window.__eventLog()");

        Assert.Multiple(() =>
        {
            Assert.That(selection, Is.EqualTo("files:"), "A cancelled picker must leave the file input empty");
            Assert.That(events, Is.EqualTo("events:"), "A cancelled picker must not raise input or change events");
        });
    }

    [Test, CancelAfter(60000), Timeout(60000)]
    public async Task FilePicker_untrustedClick_neverOpensTheNativePicker()
    {
        var picked = await ResolvePlatformFilesAsync(_notePath);
        var requests = await CaptureFilePickerAsync(picked);

        // A page can script a click on its own file input. Honouring that would let any page
        // running in a workspace pop a native file chooser without the user touching anything.
        Assert.That(
            await _desktop.EvalAsync("document.getElementById('single').click(); 'clicked'"),
            Is.EqualTo("clicked"));

        // Drive a genuine request afterwards: once it completes, the WebView message pipe has
        // been drained, so the scripted click above provably produced no request of its own.
        Assert.That(await _desktop.EvalAsync("window.__pick('single','req-after-click',false)"), Is.EqualTo("sent"));
        await _desktop.WaitForScriptAsync(
            "window.__pending()",
            "0",
            "The follow-up request never completed, so the scripted click could not be ruled out");

        Assert.That(requests, Has.Count.EqualTo(1),
            "Only the trusted-equivalent bridge message may open the native picker");
    }

    // Resolves real IStorageFile handles from the running platform's storage provider — the XDG
    // portal or GTK provider on Linux, AppKit on macOS, Win32 on Windows — so the upload bridge
    // reads the same kind of file objects a real chooser hands it.
    private async Task<IReadOnlyList<IStorageFile>> ResolvePlatformFilesAsync(params string[] paths)
    {
        var resolved = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var files = new List<IStorageFile>(paths.Length);
            foreach (var path in paths)
            {
                var file = await _desktop.Window.StorageProvider.TryGetFileFromPathAsync(path);
                Assert.That(file, Is.Not.Null,
                    $"The {RuntimeInformation.OSDescription} storage provider could not resolve '{path}'");
                files.Add(file!);
            }

            return files;
        });

        return resolved;
    }

    private async Task<List<FilePickerOpenOptions>> CaptureFilePickerAsync(IReadOnlyList<IStorageFile> selection)
    {
        var requests = new List<FilePickerOpenOptions>();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _desktop.Window.FilePicker = options =>
            {
                requests.Add(options);
                return Task.FromResult(selection);
            };
        });

        return requests;
    }
}
