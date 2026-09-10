using System.Text;
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

    // Avalonia's IStorageFile cannot be implemented outside Avalonia, so the file-reading
    // contract is covered here at its stream boundary and end to end against the real platform
    // storage provider in AgentUp.Tests.
    [Test]
    public async Task ReadBoundedContentAsync_encodesTheWholeStreamAsBase64()
    {
        var content = Encoding.UTF8.GetBytes("agent-up upload");
        using var source = new CountingStream(content, content.Length);

        var result = await WebViewFilePickerProvider.ReadBoundedContentAsync(
            source,
            WebViewFilePickerProvider.MaximumFileBytes);

        Assert.Multiple(() =>
        {
            Assert.That(result.Base64, Is.EqualTo(Convert.ToBase64String(content)));
            Assert.That(result.Bytes, Is.EqualTo(content.Length));
        });
    }

    // A picked file is whatever the user chose, so an oversized one must be refused while it is
    // being read. Reading it whole first and rejecting it afterwards would let a single
    // selection exhaust Desktop's memory before the limit ever applied.
    [Test]
    public void ReadBoundedContentAsync_refusesAnOversizedStreamBeforeBufferingAllOfIt()
    {
        using var source = new CountingStream(null, 512L * 1024 * 1024);

        Assert.ThrowsAsync<InvalidDataException>(() => WebViewFilePickerProvider.ReadBoundedContentAsync(
            source,
            WebViewFilePickerProvider.MaximumFileBytes));

        Assert.That(source.BytesRead, Is.LessThan(2 * WebViewFilePickerProvider.MaximumFileBytes),
            "The read must stop near the per-file limit instead of draining the whole stream");
    }

    // Produces the requested number of bytes without ever materialising them, so the oversized
    // case can be covered without allocating half a gigabyte in the test.
    private sealed class CountingStream(byte[]? content, long length) : Stream
    {
        private long _position;

        internal long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = length - _position;
            if (remaining <= 0)
                return 0;

            var read = (int)Math.Min(count, remaining);
            if (content is null)
                Array.Clear(buffer, offset, read);
            else
                Array.Copy(content, _position, buffer, offset, read);

            _position += read;
            BytesRead += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
