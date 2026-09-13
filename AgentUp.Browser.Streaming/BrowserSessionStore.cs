using System.Collections.Concurrent;
using System.Threading.Channels;
using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Browser.Streaming;

public sealed class BrowserSessionStore
{
    private readonly ConcurrentDictionary<string, Channel<BrowserCommandDto>> _queues = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<BrowserCommandResultDto>> _pending = new();
    private readonly ConcurrentDictionary<string, BrowserNavigationRequest> _latestNavigations = new();

    public async Task<BrowserCommandResultDto> DispatchAsync(
        BrowserCommandDto command,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<BrowserCommandResultDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[command.CommandId] = tcs;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);

        var channel = _queues.GetOrAdd(command.WorkspaceId,
            _ => Channel.CreateBounded<BrowserCommandDto>(new BoundedChannelOptions(10)
            {
                FullMode = BoundedChannelFullMode.Wait
            }));

        try
        {
            await channel.Writer.WriteAsync(command, linked.Token);
        }
        catch (ChannelClosedException)
        {
            _pending.TryRemove(command.CommandId, out _);
            return Failed(command, "Browser command queue is closed.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _pending.TryRemove(command.CommandId, out _);
            return Failed(command, "Request was cancelled.");
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(command.CommandId, out _);
            return Timeout(command);
        }

        if (command.Kind == BrowserCommandKind.Navigate)
            RegisterNavigation(command, ct);

        try
        {
            return await tcs.Task.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _pending.TryRemove(command.CommandId, out _);
            return Failed(command, "Request was cancelled.");
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(command.CommandId, out _);
            return Timeout(command);
        }
    }

    public async Task<BrowserCommandDto?> TryDequeueAsync(
        IReadOnlyList<string> workspaceIds,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (!ct.IsCancellationRequested && DateTimeOffset.UtcNow < deadline)
        {
            var command = TryReadPending(workspaceIds);
            if (command is not null)
                return command;

            try
            {
                await Task.Delay(50, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return null;
            }
        }

        return null;
    }

    public IEnumerable<string> WorkspaceIds => _queues.Keys;

    public void CompleteCommand(BrowserCommandResultDto result)
    {
        if (_pending.TryRemove(result.CommandId, out var tcs))
            tcs.TrySetResult(result);
    }

    public CancellationToken SupersessionToken(BrowserCommandDto command)
    {
        if (command.Kind != BrowserCommandKind.Navigate)
            return CancellationToken.None;
        if (_latestNavigations.TryGetValue(command.WorkspaceId, out var request)
            && request.CommandId == command.CommandId)
            return request.Cancellation.Token;
        return _latestNavigations.ContainsKey(command.WorkspaceId)
            ? new CancellationToken(canceled: true)
            : CancellationToken.None;
    }

    private void RegisterNavigation(BrowserCommandDto command, CancellationToken requestCancellation)
    {
        var replacement = new BrowserNavigationRequest(
            command.CommandId,
            CancellationTokenSource.CreateLinkedTokenSource(requestCancellation));
        _latestNavigations.AddOrUpdate(
            command.WorkspaceId,
            replacement,
            (_, previous) =>
            {
                previous.Cancellation.Cancel();
                return replacement;
            });
    }

    private BrowserCommandDto? TryReadPending(IReadOnlyList<string> workspaceIds)
        => workspaceIds
            .Select(id => _queues.TryGetValue(id, out var queue) ? queue : null)
            .Where(queue => queue is not null)
            .Select(TryReadPending)
            .FirstOrDefault(command => command is not null);

    private BrowserCommandDto? TryReadPending(Channel<BrowserCommandDto>? queue)
    {
        while (queue!.Reader.TryRead(out var command))
        {
            if (_pending.ContainsKey(command.CommandId))
                return command;
        }

        return null;
    }

    private static BrowserCommandResultDto Failed(BrowserCommandDto command, string error) =>
        new(command.CommandId, false, null, error);

    private static BrowserCommandResultDto Timeout(BrowserCommandDto command) =>
        Failed(command, "Browser command timed out. Ensure the headless browser session is running.");

}
