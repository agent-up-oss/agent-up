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

        var channel = _queues.GetOrAdd(command.WorkspaceId,
            _ => Channel.CreateBounded<BrowserCommandDto>(new BoundedChannelOptions(10)
            {
                FullMode = BoundedChannelFullMode.Wait
            }));

        try
        {
            await channel.Writer.WriteAsync(command, ct);
        }
        catch (ChannelClosedException)
        {
            _pending.TryRemove(command.CommandId, out _);
            throw;
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(command.CommandId, out _);
            throw;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);

        try
        {
            return await tcs.Task.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _pending.TryRemove(command.CommandId, out _);
            return new BrowserCommandResultDto(command.CommandId, false, null, "Request was cancelled.");
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(command.CommandId, out _);
            return new BrowserCommandResultDto(command.CommandId, false, null,
                "Desktop app did not respond within the timeout. Ensure the Agent-Up Desktop app is open with the workspace browser visible.");
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
            var command = workspaceIds
                .Select(id => _queues.TryGetValue(id, out var queue) ? queue : null)
                .Where(queue => queue is not null)
                .Select(queue => queue!.Reader.TryRead(out var cmd) ? cmd : null)
                .FirstOrDefault(cmd => cmd is not null);
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

    public void CompleteCommand(BrowserCommandResultDto result)
    {
        if (_pending.TryRemove(result.CommandId, out var tcs))
            tcs.TrySetResult(result);
    }
}
