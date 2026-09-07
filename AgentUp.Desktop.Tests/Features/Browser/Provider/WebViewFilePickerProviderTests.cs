using System.Text.Json;
using AgentUp.Desktop.Features.Browser.Providers;

namespace AgentUp.Desktop.Tests.Features.Browser.Provider;

public sealed class WebViewFilePickerProviderTests
{
    [Test]
    public void TryParseRequest_acceptsOwnedMessage()
    {
        var parsed = WebViewFilePickerProvider.TryParseRequest(
            "{\"type\":\"agent-up:file-picker\",\"requestId\":\"pick-1\",\"multiple\":true}",
            out var request);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(request?.RequestId, Is.EqualTo("pick-1"));
            Assert.That(request?.Multiple, Is.True);
        });
    }

    [TestCase("not json")]
    [TestCase("{\"type\":\"some-app-message\",\"requestId\":\"pick-1\"}")]
    [TestCase("{\"type\":\"agent-up:file-picker\",\"requestId\":\"\"}")]
    public void TryParseRequest_ignoresMalformedOrUnownedMessages(string body)
    {
        Assert.That(WebViewFilePickerProvider.TryParseRequest(body, out _), Is.False);
    }

    [Test]
    public void CompleteScript_serializesNamesSafelyAndDispatchesFileInputEvents()
    {
        var script = WebViewFilePickerProvider.CompleteScript(
            "request-'1",
            [new("quote'file.txt", "text/plain", "aGVsbG8=")]);

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain(JsonSerializer.Serialize("request-'1")));
            Assert.That(script, Does.Contain("quote\\u0027file.txt"));
            Assert.That(script, Does.Contain("new DataTransfer()"));
            Assert.That(script, Does.Contain("new Event('input'"));
            Assert.That(script, Does.Contain("new Event('change'"));
        });
    }

    [Test]
    public void InstallScript_interceptsRegularInputsButLeavesDirectoryInputsToTheEngine()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WebViewFilePickerProvider.InstallScript, Does.Contain("input[type=file]"));
            Assert.That(WebViewFilePickerProvider.InstallScript, Does.Contain("e.isTrusted"));
            Assert.That(WebViewFilePickerProvider.InstallScript, Does.Contain("input.webkitdirectory"));
            Assert.That(WebViewFilePickerProvider.InstallScript, Does.Contain("invokeCSharpAction"));
        });
    }
}
