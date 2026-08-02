using System.Collections.Concurrent;
using System.Threading.Channels;
using AgentUp.Server.Features.Browser.Models;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserSessionStore
{
    private readonly ConcurrentDictionary<string, Channel<BrowserCommandDto>> _queues = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<BrowserCommandResultDto>> _pending = new();

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
        Failed(command,
            "Browser command timed out. Ensure the server is configured with Browser:Mode=headless and the headless browser session is running.");
}
