using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AcpProcessProvider(AgentCommandProvider commands, ILogger<AcpProcessProvider> logger) : IAgentProcessProvider
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _writes = new(1, 1);
    private Process? _process;
    private Task? _reader;
    private Task? _errorReader;
    private readonly CancellationTokenSource _lifetime = new();
    private long _nextId;

    public event Func<string, JsonElement, Task>? Notification;
    public event Func<string, JsonElement, Task<JsonElement>>? Request;
    public event Action<string?>? Exited;

    public Task StartAsync(AgentKind kind, string workingDirectory, CancellationToken cancellationToken)
    {
        if (_process is not null) throw new InvalidOperationException("The ACP process has already started.");
        var command = commands.Get(kind);
        var start = new ProcessStartInfo(command.FileName) {
            WorkingDirectory = workingDirectory, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        foreach (var argument in command.Arguments) start.ArgumentList.Add(argument);
        var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException($"Could not start {command.FileName}.");
            }
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            process.Dispose();
            throw new InvalidOperationException($"Could not start the configured ACP executable '{command.FileName}'.", exception);
        }
        _process = process;
        _reader = ReadLoopAsync(process, _lifetime.Token);
        _errorReader = ReadErrorsAsync(process, _lifetime.Token);
        return Task.CompletedTask;
    }

    public async Task<JsonElement> CallAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion)) throw new InvalidOperationException("Could not allocate an ACP request ID.");
        try {
            await WriteAsync(new { jsonrpc = "2.0", id, method, @params = parameters }, cancellationToken);
            return await completion.Task.WaitAsync(cancellationToken);
        } finally { _pending.TryRemove(id, out _); }
    }

    public Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken) =>
        WriteAsync(new { jsonrpc = "2.0", method, @params = parameters }, cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var process = _process;
        if (process is null || process.HasExited) return;
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try { await process.WaitForExitAsync(stop.Token); }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }
    }

    private async Task WriteAsync(object value, CancellationToken cancellationToken)
    {
        var process = _process ?? throw new InvalidOperationException("The ACP process is not running.");
        var line = JsonSerializer.Serialize(value);
        await _writes.WaitAsync(cancellationToken);
        try { await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken); await process.StandardInput.FlushAsync(cancellationToken); }
        finally { _writes.Release(); }
    }

    private async Task ReadLoopAsync(Process process, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            try { using var document = JsonDocument.Parse(line); await DispatchAsync(document.RootElement.Clone(), cancellationToken); }
            catch (JsonException exception) { logger.LogWarning(exception, "ACP agent emitted invalid JSON: {Line}", line); }
        }
        if (!cancellationToken.IsCancellationRequested)
        {
            await process.WaitForExitAsync(cancellationToken);
            HandleExit(process);
        }
    }

    private async Task DispatchAsync(JsonElement message, CancellationToken cancellationToken)
    {
        if (message.TryGetProperty("id", out var idElement) && !message.TryGetProperty("method", out _))
        {
            var id = idElement.GetInt64();
            if (!_pending.TryGetValue(id, out var completion)) return;
            if (message.TryGetProperty("error", out var error)) completion.TrySetException(new InvalidOperationException(error.ToString()));
            else completion.TrySetResult(message.GetProperty("result").Clone());
            return;
        }
        if (!message.TryGetProperty("method", out var methodElement)) return;
        var method = methodElement.GetString() ?? "unknown";
        var parameters = message.TryGetProperty("params", out var value) ? value.Clone() : JsonSerializer.SerializeToElement(new { });
        if (message.TryGetProperty("id", out var requestId))
        {
            try {
                var result = Request is null ? JsonSerializer.SerializeToElement(new { }) : await Request(method, parameters);
                await WriteAsync(new { jsonrpc = "2.0", id = requestId.Clone(), result }, cancellationToken);
            } catch (Exception exception) when (exception is InvalidOperationException or IOException) {
                await WriteAsync(new { jsonrpc = "2.0", id = requestId.Clone(), error = new { code = -32603, message = exception.Message } }, cancellationToken);
            }
        }
        else if (Notification is not null) await Notification(method, parameters);
    }

    private async Task ReadErrorsAsync(Process process, CancellationToken cancellationToken)
    {
        while (await process.StandardError.ReadLineAsync(cancellationToken) is { } line) logger.LogInformation("ACP: {Line}", line);
    }

    private void HandleExit(Process process)
    {
        var error = process.ExitCode == 0 ? null : $"Agent process exited with code {process.ExitCode}.";
        foreach (var completion in _pending.Values) completion.TrySetException(new InvalidOperationException(error ?? "Agent process exited."));
        Exited?.Invoke(error);
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await StopAsync(timeout.Token); } catch (OperationCanceledException) when (timeout.IsCancellationRequested) { logger.LogDebug("Timed out while stopping the ACP process."); }
        if (_reader is not null) try { await _reader; } catch (OperationCanceledException exception) { logger.LogDebug(exception, "ACP reader was cancelled."); }
        if (_errorReader is not null) try { await _errorReader; } catch (OperationCanceledException exception) { logger.LogDebug(exception, "ACP error reader was cancelled."); }
        _process?.Dispose(); _writes.Dispose();
        _lifetime.Dispose();
    }
}
