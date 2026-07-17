using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
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
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
            await File.WriteAllTextAsync(outputFile, body, cancellationToken);
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
