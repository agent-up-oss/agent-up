using AgentUp.AUDebug.Features.Host.Interfaces;

namespace AgentUp.AUDebug.Features.Host.Providers;

public sealed class HostReadyProbeProvider : IHostReadyProbe
{
    private readonly HttpClient _http;

    public HostReadyProbeProvider(HttpClient http) => _http = http;

    public async Task WaitAsync(string url, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await CheckAsync(url, cancellationToken))
                return;

            await Task.Delay(200, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<bool> CheckAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            return (int)response.StatusCode is >= 200 and < 500;
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
}
