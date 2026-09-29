using System.Collections.Concurrent;
using System.Text;

namespace AgentUp.Desktop.Features.FakeServer.Providers;

public sealed class FakeServerSseStream : Stream
{
    private readonly ConcurrentQueue<byte[]> _chunks = new();
    private readonly SemaphoreSlim _available = new(0);
    private byte[] _current = [];
    private int _consumed;
    private int _completed;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public void WriteFrame(string frame)
    {
        if (Volatile.Read(ref _completed) == 1)
            return;
        _chunks.Enqueue(Encoding.UTF8.GetBytes(frame));
        _available.Release();
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 1)
            return;
        _available.Release();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (_consumed >= _current.Length)
        {
            if (_chunks.TryDequeue(out var next))
            {
                _current = next;
                _consumed = 0;
                continue;
            }

            if (Volatile.Read(ref _completed) == 1)
                return 0;

            await _available.WaitAsync(cancellationToken);
        }

        var copied = Math.Min(buffer.Length, _current.Length - _consumed);
        _current.AsSpan(_consumed, copied).CopyTo(buffer.Span);
        _consumed += copied;
        return copied;
    }

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("The fake Server event stream is asynchronous.");

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Complete();
            _available.Dispose();
        }

        base.Dispose(disposing);
    }
}
