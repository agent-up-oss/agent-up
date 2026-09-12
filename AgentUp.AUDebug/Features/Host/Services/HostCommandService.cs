using AgentUp.AUDebug.Features.Desktop.Interfaces;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;

namespace AgentUp.AUDebug.Features.Host.Services;

public sealed class HostCommandService
{
    private readonly IHostSessionStore _sessions;
    private readonly IHostProcessSupervisor _supervisor;
    private readonly IHostReadyProbe _probe;
    private readonly IDesktopWindowDriver _windows;
    private readonly DebugOutputService _output;

    public HostCommandService(
        IHostSessionStore sessions,
        IHostProcessSupervisor supervisor,
        IHostReadyProbe probe,
        IDesktopWindowDriver windows,
        DebugOutputService output)
    {
        _sessions = sessions;
        _supervisor = supervisor;
        _probe = probe;
        _windows = windows;
        _output = output;
    }

    public async Task<CommandResultDto> UpAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        var existing = _sessions.Read();
        if (existing is not null && _supervisor.HasLiveProcess(existing))
            return CommandResultDto.Ok(FormatReady(existing, alreadyUp: true));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        HostSessionDto? session = null;
        try
        {
            session = await _supervisor.StartAsync(timeout.Token);
            _sessions.Write(session);
            await WaitUntilReadyAsync(timeout.Token);
            var ready = FormatReady(session, alreadyUp: false);
            if (command.Detach)
                return CommandResultDto.Ok(ready);

            _output.WriteMessage(ready);
            await _supervisor.WaitAsync(cancellationToken);
            await StopSessionAsync(session);
            return CommandResultDto.Ok("au-debug stopped.");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            if (session is not null)
                await StopSessionAsync(session);
            return CommandResultDto.Fail(TimeoutMessage(session, command.Timeout));
        }
    }

    public async Task<CommandResultDto> DownAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            var session = _sessions.Read();
            if (session is null)
                return CommandResultDto.Ok("au-debug is not running.");

            await _supervisor.StopAsync(session, timeout.Token);
            _sessions.Delete();
            return CommandResultDto.Ok("Stopped desktop, mobile, docs, and the repo Server.");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s waiting for au-debug down.");
        }
    }

    public async Task<CommandResultDto> StatusAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            var session = _sessions.Read();
            if (session is null || !_supervisor.HasLiveProcess(session))
                return CommandResultDto.Fail("au-debug is not running.");

            var server = await _probe.CheckAsync(DebugLayout.ServerUrl + DebugLayout.ServerReadyPath, timeout.Token);
            var mobile = await _probe.CheckAsync(DebugLayout.MobileUrl, timeout.Token);
            var docs = await _probe.CheckAsync(DebugLayout.DocsUrl, timeout.Token);
            var desktop = await _windows.HasWindowAsync(timeout.Token);
            var lines = new[]
            {
                "au-debug running",
                $"server: {DebugLayout.ServerUrl} {(server ? "ready" : "not ready")}",
                $"desktop: window {DebugLayout.DesktopWindowClass} {(desktop ? "present" : "missing")}",
                $"mobile: {DebugLayout.MobileUrl} {(mobile ? "ready" : "not ready")}",
                $"docs: {DebugLayout.DocsUrl} {(docs ? "ready" : "not ready")}",
            };
            var message = string.Join(Environment.NewLine, lines);
            return server && mobile && docs && desktop
                ? CommandResultDto.Ok(message)
                : CommandResultDto.Fail(message);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s checking au-debug status.");
        }
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            _probe.WaitAsync(DebugLayout.ServerUrl + DebugLayout.ServerReadyPath, cancellationToken),
            _probe.WaitAsync(DebugLayout.MobileUrl, cancellationToken),
            _probe.WaitAsync(DebugLayout.DocsUrl, cancellationToken),
            _windows.WaitForWindowAsync(cancellationToken));
    }

    private async Task StopSessionAsync(HostSessionDto session)
    {
        await _supervisor.StopAsync(session, CancellationToken.None);
        _sessions.Delete();
    }

    private string TimeoutMessage(HostSessionDto? session, TimeSpan timeout)
    {
        var logs = session is null
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                session.Processes.Select(process => $"--- {process.Name} ---\n{_sessions.ReadLogTail(process.LogPath, 40)}"));
        return $"Timed out after {(int)timeout.TotalSeconds}s waiting for au-debug up to become ready.{Environment.NewLine}{logs}";
    }

    private static string FormatReady(HostSessionDto session, bool alreadyUp)
    {
        var prefix = alreadyUp ? "au-debug already running" : "au-debug ready";
        var lines = session.Processes
            .Select(process => process.Url is null
                ? $"{process.Name}: pid {process.Pid} log {process.LogPath}"
                : $"{process.Name}: {process.Url} (pid {process.Pid}) log {process.LogPath}");
        return string.Join(
            Environment.NewLine,
            new[] { prefix, $"logs: {session.SessionDirectory}" }.Concat(lines));
    }
}
