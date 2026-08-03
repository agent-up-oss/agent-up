namespace AgentUp.Desktop.Shared.Interfaces;

internal interface IBrowserWindowHost
{
    Task<IReadOnlyCollection<string>> GetActiveWorkspaceIdsAsync();
    Task<string?> EvalAsync(string workspaceId, string script);
    Task<bool> ActivateWorkspaceUrlAsync(string workspaceId, string url) =>
        Task.FromResult(false);
    bool NavigateTo(string workspaceId, string? url);
}
