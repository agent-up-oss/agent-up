namespace AgentUp.Desktop.Features.Browser.Controllers;

// Public entry point for other slices to request browser viewport changes without
// importing MainWindow directly. Wired up in MainWindow using method-group delegates
// so the controller carries no reference to the view layer.
public sealed class BrowserViewportController
{
    private readonly Action<string, string?> _navigateTo;
    private readonly Func<string, string, Task<string?>> _evalAsync;

    internal BrowserViewportController(
        Action<string, string?> navigateTo,
        Func<string, string, Task<string?>> evalAsync)
    {
        _navigateTo = navigateTo;
        _evalAsync = evalAsync;
    }

    public void NavigateTo(string workspaceId, string? url) => _navigateTo(workspaceId, url);

    public Task<string?> EvalAsync(string workspaceId, string script) => _evalAsync(workspaceId, script);
}
