using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Providers;

public sealed class TrainingApiClient(HttpClient http) : ITrainingApiProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<DecisionRepository>> ListRepositoriesAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/training/repositories", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<DecisionRepository>>(Options, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<ProductDecision>> ListDecisionsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"api/training/decisions?owner={Uri.EscapeDataString(owner)}&repository={Uri.EscapeDataString(repository)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ProductDecision>>(Options, cancellationToken) ?? [];
    }

    public async Task<DecisionEvidenceImage?> EvidenceAsync(string evidenceId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"api/training/evidence/{Uri.EscapeDataString(evidenceId)}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<DecisionEvidenceImage>(Options, cancellationToken);
    }

    public async Task ReviewAsync(string decisionId, string verdict, string? note, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/training/decisions/{Uri.EscapeDataString(decisionId)}/review",
            new { verdict, note },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> ExportAsync(bool includePending, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"api/training/export?includePending={(includePending ? "true" : "false")}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
