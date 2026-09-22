using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Features.Capabilities.Interfaces;

namespace AgentUp.Desktop.Features.Capabilities.Providers;

public sealed class CapabilityModulesApiClient(HttpClient http) : ICapabilityModulesApiProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("/api/capabilities", cancellationToken);
        response.EnsureSuccessStatusCode();
        var modules = await response.Content.ReadFromJsonAsync<List<CapabilityModuleDto>>(Options, cancellationToken);
        return modules ?? [];
    }

    public async Task<CapabilityModuleDto> EnableAsync(
        string id,
        string? version,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/capabilities/enable",
            new EnableCapabilityModuleRequestDto(id, version),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadModuleAsync(response, cancellationToken);
    }

    public async Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync(
            $"/api/capabilities/disable/{Uri.EscapeDataString(id)}",
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadModuleAsync(response, cancellationToken);
    }

    private static async Task<CapabilityModuleDto> ReadModuleAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var module = await response.Content.ReadFromJsonAsync<CapabilityModuleDto>(Options, cancellationToken);
        return module ?? throw new InvalidOperationException("The server returned an empty capability module.");
    }
}
