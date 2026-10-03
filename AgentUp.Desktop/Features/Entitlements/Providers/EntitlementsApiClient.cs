using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Entitlements.DTOs;

namespace AgentUp.Desktop.Features.Entitlements.Providers;

public sealed class EntitlementsApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<EntitlementsDocumentDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await http.GetFromJsonAsync<EntitlementsDocumentDto>("/api/entitlements", Options, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}
