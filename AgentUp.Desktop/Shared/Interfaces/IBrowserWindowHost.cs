namespace AgentUp.Desktop.Shared.Interfaces;

internal interface IBrowserWindowHost
{
    Task<IReadOnlyCollection<string>> GetActiveWorkspaceIdsAsync();
    Task<string?> EvalAsync(string workspaceId, string script);
    bool NavigateTo(string workspaceId, string? url);
}
