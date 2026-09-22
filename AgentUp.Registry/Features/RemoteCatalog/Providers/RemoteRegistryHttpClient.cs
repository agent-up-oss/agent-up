using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.RemoteCatalog.DTOs;
using AgentUp.Registry.Features.RemoteCatalog.Interfaces;

namespace AgentUp.Registry.Features.RemoteCatalog.Providers;

public sealed class RemoteRegistryHttpClient(HttpClient http) : IRemoteRegistryClient
{
    public async Task<IReadOnlyList<CapabilityRegistryIndexEntry>> ListAsync(CancellationToken cancellationToken)
    {
        var list = await http.GetFromJsonAsync<RemoteCatalogListDto>("packages", cancellationToken);
        return list?.Packages ?? [];
    }

    public async Task<RemotePackageBytesDto> DownloadAsync(string id, string version, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"packages/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(version)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new RemotePackageBytesDto(id, version, bytes);
    }

    public async Task PushAsync(RemotePackageBytesDto package, string token, CancellationToken cancellationToken)
    {
        using var content = new ByteArrayContent(package.Archive);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"packages/{Uri.EscapeDataString(package.Id)}/{Uri.EscapeDataString(package.Version)}")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
