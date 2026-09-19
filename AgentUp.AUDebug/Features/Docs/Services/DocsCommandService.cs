using System.Net.WebSockets;
using System.Text.Json;
using AgentUp.AUDebug.Features.Docs.Interfaces;
using AgentUp.AUDebug.Features.Docs.Providers;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;

namespace AgentUp.AUDebug.Features.Docs.Services;

public sealed class DocsCommandService
{
    private readonly IDocsPageCapture _pages;
    private readonly IHostSessionStore _sessions;

    public DocsCommandService(IDocsPageCapture pages, IHostSessionStore sessions)
    {
        _pages = pages;
        _sessions = sessions;
    }

    public async Task<CommandResultDto> ScreenshotAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            var path = _sessions.ScreenshotPath("docs");
            var url = DocsPageUrlProvider.Resolve(command.PagePath);
            await _pages.CaptureAsync(url, path, command.Heading, command.FullPage, timeout.Token);
            return CommandResultDto.Ok("Wrote docs screenshot.", path);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s capturing a docs screenshot.");
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                        or HttpRequestException
                                        or WebSocketException
                                        or JsonException)
        {
            return CommandResultDto.Fail(ex.Message);
        }
    }
}
