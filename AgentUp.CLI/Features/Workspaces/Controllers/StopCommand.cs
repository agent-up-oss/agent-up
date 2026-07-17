using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class StopCommand
{
    private readonly WorkspaceApiClient _client;
    private readonly CurrentWorkspaceResolver _resolver;
    private readonly TextWriter _output;

    public StopCommand(
        WorkspaceApiClient client,
        CurrentWorkspaceResolver resolver,
        TextWriter output)
    {
        _client = client;
        _resolver = resolver;
        _output = output;
    }

    public async Task<int> RunAsync()
    {
        var resolution = await _resolver.ResolveAsync(
            "Error: Failed to connect to server",
            "Error: No workspace found for the current directory. Run 'agent-up start' first.");
        if (!resolution.Succeeded)
        {
            _output.WriteLine(resolution.Error);
            return 1;
        }

        var workspace = resolution.Workspace!;
        try
        {
            await _client.StopWorkspaceAsync(workspace.Id);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        _output.WriteLine($"Stopped workspace \"{workspace.DisplayName}\"");
        return 0;
    }
}
