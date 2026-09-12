using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Docs.Services;

public sealed class DocsCommandService
{
    private readonly IWebScreenshotDriver _screenshots;
    private readonly IHostSessionStore _sessions;

    public DocsCommandService(IWebScreenshotDriver screenshots, IHostSessionStore sessions)
    {
        _screenshots = screenshots;
        _sessions = sessions;
    }

    public async Task<CommandResultDto> ScreenshotAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            var path = _sessions.ScreenshotPath("docs");
            await _screenshots.CaptureAsync($"{DebugLayout.DocsUrl}{DebugLayout.DocsPath}", path, timeout.Token);
            return CommandResultDto.Ok("Wrote docs screenshot.", path);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s capturing a docs screenshot.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            return CommandResultDto.Fail(ex.Message);
        }
    }
}
