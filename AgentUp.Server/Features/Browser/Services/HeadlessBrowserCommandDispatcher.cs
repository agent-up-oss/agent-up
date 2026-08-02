using AgentUp.Server.Features.Browser.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class HeadlessBrowserCommandDispatcher(
    BrowserSessionStore store,
    HeadlessBrowserSessionManager sessionManager,
    CdpBrowserExecutor executor,
    ILogger<HeadlessBrowserCommandDispatcher> logger)
    : IHostedService
{
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public Task StartAsync(CancellationToken ct)
    {
        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_cts is null) return;
        await _cts.CancelAsync();
        if (_loop is not null)
            try { await _loop.WaitAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var ids = store.WorkspaceIds.ToList();
            if (ids.Count == 0)
            {
                await DelayAsync(200, ct);
                continue;
            }

            BrowserCommandDto? command;
            try
            {
                command = await store.TryDequeueAsync(ids, TimeSpan.FromMilliseconds(500), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (command is null) continue;

            _ = Task.Run(() => ExecuteAndCompleteAsync(command, ct), ct);
        }
    }

    private async Task ExecuteAndCompleteAsync(BrowserCommandDto command, CancellationToken ct)
    {
        BrowserCommandResultDto result;
        try
        {
            var session = command.Kind == BrowserCommandKind.Navigate
                ? await sessionManager.EnsureSessionAsync(command.WorkspaceId, ct)
                : sessionManager.GetSession(command.WorkspaceId);

            result = session is null
                ? Fail(command, $"No browser session for workspace '{command.WorkspaceId}'. Navigate to a URL first.")
                : await executor.ExecuteAsync(session, command, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is PuppeteerException or ProcessException or InvalidOperationException or IOException or TimeoutException)
        {
            logger.LogError(ex, "Error executing browser command {CommandId}.", command.CommandId);
            result = Fail(command, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error executing browser command {CommandId}.", command.CommandId);
            result = Fail(command, ex.Message);
        }

        store.CompleteCommand(result);
    }

    private static BrowserCommandResultDto Fail(BrowserCommandDto command, string error) =>
        new(command.CommandId, false, null, error);

    private static async Task DelayAsync(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
    }
}
