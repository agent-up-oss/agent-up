using System.Text;
using System.Text.Json;
using AgentUp.Desktop.Features.Browser.Providers;
using Avalonia.Platform.Storage;

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

    [Test]
    public async Task ReadFilesAsync_encodesEachSelectedFileWithItsMimeType()
    {
        var files = await WebViewFilePickerProvider.ReadFilesAsync(
        [
            new StubStorageFile("notes.txt", Encoding.UTF8.GetBytes("hello")),
            new StubStorageFile("logo.png", [0x89, 0x50, 0x4E, 0x47])
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(files.Select(file => file.Name), Is.EqualTo(new[] { "notes.txt", "logo.png" }));
            Assert.That(files.Select(file => file.MimeType), Is.EqualTo(new[] { "text/plain", "image/png" }));
            Assert.That(files[0].Base64Content, Is.EqualTo(Convert.ToBase64String(Encoding.UTF8.GetBytes("hello"))));
            Assert.That(files[1].Base64Content, Is.EqualTo(Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 })));
        });
    }

    // A picked file is whatever the user chose, so an oversized one must be refused while it is
    // being read. Reading it whole first and rejecting it afterwards would let a single
    // selection exhaust Desktop's memory before the limit ever applied.
    [Test]
    public void ReadFilesAsync_refusesAnOversizedFileBeforeBufferingAllOfIt()
    {
        const long fileSize = 512L * 1024 * 1024;
        var oversized = new StubStorageFile("huge.bin", fileSize);

        Assert.ThrowsAsync<InvalidDataException>(() => WebViewFilePickerProvider.ReadFilesAsync([oversized]));
        Assert.That(oversized.BytesRead, Is.LessThan(64L * 1024 * 1024),
            "The read must stop near the 32 MB per-file limit instead of draining the whole file");
    }

    private sealed class StubStorageFile : IStorageFile
    {
        private readonly byte[]? _content;
        private readonly long _length;
        private CountingStream? _stream;

        internal StubStorageFile(string name, byte[] content)
        {
            Name = name;
            _content = content;
            _length = content.Length;
        }

        internal StubStorageFile(string name, long length)
        {
            Name = name;
            _length = length;
        }

        internal long BytesRead => _stream?.BytesRead ?? 0;

        public string Name { get; }

        public Uri Path => new($"file:///selected/{Name}");

        public bool CanBookmark => false;

        public Task<StorageItemProperties> GetBasicPropertiesAsync()
            => Task.FromResult(new StorageItemProperties((ulong)_length));

        public Task<string?> SaveBookmarkAsync() => Task.FromResult<string?>(null);

        public Task<IStorageFolder?> GetParentAsync() => Task.FromResult<IStorageFolder?>(null);

        public Task DeleteAsync() => Task.CompletedTask;

        public Task<IStorageItem?> MoveAsync(IStorageFolder destination) => Task.FromResult<IStorageItem?>(null);

        public Task<Stream> OpenReadAsync()
        {
            _stream = new CountingStream(_content, _length);
            return Task.FromResult<Stream>(_stream);
        }

        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();

        public void Dispose() => _stream?.Dispose();
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
