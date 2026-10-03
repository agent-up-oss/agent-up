using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>Readiness for the two things a screens run waits on: a served page and an X11 window.</summary>
public sealed class ScreenReadyProbeProvider : IScreenReadyProbe
{
    private readonly HttpClient _http;
    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugEnvironment _environment;
    private readonly IDebugPathValidator _paths;

    public ScreenReadyProbeProvider(
        HttpClient http,
        IAllowlistedProcessRunner processes,
        IDebugEnvironment environment,
        IDebugPathValidator paths)
    {
        _http = http;
        _processes = processes;
        _environment = environment;
        _paths = paths;
    }

    public async Task<bool> IsReadyAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task WaitForUrlAsync(string url, CancellationToken cancellationToken)
    {
        while (!await IsReadyAsync(url, cancellationToken))
            await Task.Delay(500, cancellationToken);
    }

    public async Task<bool> HasDesktopWindowAsync(CancellationToken cancellationToken)
    {
        var result = await X11ToolRunner.RunAsync(
            _processes,
            _environment,
            _paths.RepositoryRoot,
            "xdotool",
            "xdotool",
            ["search", "--onlyvisible", "--class", DebugLayout.DesktopWindowClass],
            cancellationToken);
        return result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length > 0;
    }

    public async Task WaitForDesktopWindowAsync(CancellationToken cancellationToken)
    {
        while (!await HasDesktopWindowAsync(cancellationToken))
            await Task.Delay(500, cancellationToken);
    }
}
