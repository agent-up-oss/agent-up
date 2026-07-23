using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Shared.Providers;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;

public sealed class HttpServerProbe : IServerProbe
{
    private readonly HttpClient _httpClient = new();

    public async Task<string?> WaitForReadyAsync(
        string primaryUrl,
        string fallbackUrl,
        string outputFile,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 90; attempt++)
        {
            var readyUrl = await TryFetchAsync(primaryUrl, outputFile, cancellationToken)
                ?? await TryFetchAsync(fallbackUrl, outputFile, cancellationToken);
            if (readyUrl is not null)
                return readyUrl;

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return null;
    }

    private async Task<string?> TryFetchAsync(string baseUrl, string outputFile, CancellationToken cancellationToken)
    {
        try
        {
            var body = await _httpClient.GetStringAsync($"{baseUrl.TrimEnd('/')}/api/workspaces", cancellationToken);
            var safeOutputFile = SafeSmokePaths.Child(Path.GetDirectoryName(outputFile)!, Path.GetFileName(outputFile));
            Directory.CreateDirectory(Path.GetDirectoryName(safeOutputFile)!);
            await File.WriteAllTextAsync(safeOutputFile, body, cancellationToken);
            return baseUrl;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
