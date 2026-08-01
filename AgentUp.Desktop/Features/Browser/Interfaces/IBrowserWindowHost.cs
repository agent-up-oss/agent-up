namespace AgentUp.Desktop.Features.Browser.Interfaces;

internal interface IBrowserWindowHost
{
    IReadOnlyCollection<string> ActiveWorkspaceIds { get; }
    Task<string?> EvalAsync(string workspaceId, string script);
    void NavigateTo(string workspaceId, string? url);
}
