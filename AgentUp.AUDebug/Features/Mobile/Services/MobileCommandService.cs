using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;
using AgentUp.AUDebug.Features.Mobile.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Mobile.Services;

public sealed class MobileCommandService
{
    private readonly IWebScreenshotDriver _screenshots;
    private readonly IMobileSurfaceDriver _surface;
    private readonly IHostSessionStore _sessions;
    private readonly IDebugEnvironment _environment;

    public MobileCommandService(
        IWebScreenshotDriver screenshots,
        IMobileSurfaceDriver surface,
        IHostSessionStore sessions,
        IDebugEnvironment environment)
    {
        _screenshots = screenshots;
        _surface = surface;
        _sessions = sessions;
        _environment = environment;
    }

    public Task<CommandResultDto> ScreenshotAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => RunAsync(command, cancellationToken, CaptureAsync);

    public Task<CommandResultDto> LoginAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => RunAsync(command, cancellationToken, ct => LoginCoreAsync(command, ct));

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
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s running mobile {command.Action}.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or UriFormatException)
        {
            return CommandResultDto.Fail(ex.Message);
        }
    }

    private async Task<CommandResultDto> CaptureAsync(CancellationToken cancellationToken)
    {
        var path = _sessions.ScreenshotPath("mobile");
        await _screenshots.CaptureAsync($"{DebugLayout.MobileUrl}/", path, cancellationToken, _surface.UserDataDirectory);
        return CommandResultDto.Ok("Wrote Mobile screenshot.", path);
    }

    private async Task<CommandResultDto> LoginCoreAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        var password = command.Password ?? _environment.AdminPassword;
        if (string.IsNullOrWhiteSpace(password))
            return CommandResultDto.Fail("Set AGENTUP_ADMIN_PASSWORD or pass --password for mobile login.");

        await _surface.LoginAsync(DebugLayout.ServerUrl, password, cancellationToken);
        var path = _sessions.ScreenshotPath("mobile");
        await _screenshots.CaptureAsync($"{DebugLayout.MobileUrl}/", path, cancellationToken, _surface.UserDataDirectory);
        return CommandResultDto.Ok("Submitted Mobile sign-in.", path);
    }
}
