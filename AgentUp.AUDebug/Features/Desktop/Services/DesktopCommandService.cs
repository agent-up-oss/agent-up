using AgentUp.AUDebug.Features.Desktop.Interfaces;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Desktop.Services;

public sealed class DesktopCommandService
{
    private readonly IDesktopWindowDriver _windows;
    private readonly IDesktopWorkspaceClient _workspaces;
    private readonly IHostSessionStore _sessions;
    private readonly IDebugEnvironment _environment;

    public DesktopCommandService(
        IDesktopWindowDriver windows,
        IDesktopWorkspaceClient workspaces,
        IHostSessionStore sessions,
        IDebugEnvironment environment)
    {
        _windows = windows;
        _workspaces = workspaces;
        _sessions = sessions;
        _environment = environment;
    }

    public Task<CommandResultDto> ScreenshotAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => RunAsync(command, cancellationToken, CaptureAsync);

    public Task<CommandResultDto> LoginAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => RunAsync(command, cancellationToken, ct => LoginCoreAsync(command, ct));

    public Task<CommandResultDto> StartWorkspaceAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => RunAsync(command, cancellationToken, ct => StartWorkspaceCoreAsync(command, ct));

    private async Task<CommandResultDto> RunAsync(
        DebugCommandDto command,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<CommandResultDto>> action)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            return await action(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s running desktop {command.Action}.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            return CommandResultDto.Fail(ex.Message);
        }
    }

    private async Task<CommandResultDto> CaptureAsync(CancellationToken cancellationToken)
    {
        var path = _sessions.ScreenshotPath("desktop");
        await _windows.CaptureAsync(path, cancellationToken);
        return CommandResultDto.Ok($"Wrote Desktop screenshot.", path);
    }

    private async Task<CommandResultDto> LoginCoreAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        var password = command.Password ?? _environment.AdminPassword;
        if (string.IsNullOrWhiteSpace(password))
            return CommandResultDto.Fail("Set AGENTUP_ADMIN_PASSWORD or pass --password for desktop login.");

        await _windows.LoginAsync(password, cancellationToken);
        var path = _sessions.ScreenshotPath("desktop");
        await _windows.CaptureAsync(path, cancellationToken);
        return CommandResultDto.Ok("Submitted Desktop sign-in.", path);
    }

    private async Task<CommandResultDto> StartWorkspaceCoreAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        var password = command.Password ?? _environment.AdminPassword;
        if (string.IsNullOrWhiteSpace(password))
            return CommandResultDto.Fail("Set AGENTUP_ADMIN_PASSWORD or pass --password to start a workspace.");
        if (string.IsNullOrWhiteSpace(command.WorkspaceName))
            return CommandResultDto.Fail("desktop start-workspace requires a workspace name.");

        await _workspaces.StartByNameAsync(command.WorkspaceName, password, cancellationToken);
        var path = _sessions.ScreenshotPath("desktop");
        await _windows.CaptureAsync(path, cancellationToken);
        return CommandResultDto.Ok($"Started workspace '{command.WorkspaceName}'.", path);
    }
}
