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
        catch
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
            foreach (var id in workspaceIds)
            {
                if (_queues.TryGetValue(id, out var channel) && channel.Reader.TryRead(out var cmd))
                    return cmd;
            }

            try
            {
                await Task.Delay(50, ct);
            }
            catch (OperationCanceledException)
            {
                break;
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
