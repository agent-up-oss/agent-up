using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class StartCommand
{
    private readonly WorkspaceCommandService _service;
    private readonly TextWriter _output;

    public StartCommand(WorkspaceCommandService service, TextWriter output)
    {
        _service = service;
        _output = output;
    }

    public async Task<int> RunAsync()
    {
        var result = await _service.StartAsync();
        if (!result.Succeeded)
        {
            _output.WriteLine(result.Error);
            return 1;
        }

        var started = result.Value!;
        var workspace = started.Workspace;

        _output.WriteLine($"Started workspace \"{workspace.DisplayName}\"");
        _output.WriteLine($"  Branch:  {workspace.Branch}");
        _output.WriteLine($"  Commit:  {workspace.Commit}");

        if (started.Applications.Count > 0)
        {
            _output.WriteLine($"  Applications ({started.Applications.Count}):");
            foreach (var app in started.Applications)
                _output.WriteLine($"    - {app.Name}: {app.Command}");
        }

        if (started.Services.Count > 0)
        {
            _output.WriteLine($"  Services ({started.Services.Count}):");
            foreach (var svc in started.Services)
                _output.WriteLine($"    - {svc.Name}: {svc.Image}");
        }

        if (started.Dotnet.Count > 0)
        {
            _output.WriteLine($"  .NET ({started.Dotnet.Count}):");
            foreach (var app in started.Dotnet)
                _output.WriteLine($"    - {app.Name}: dotnet run --project {app.Run.Project}");
        }

        if (started.Docker.Count > 0)
        {
            _output.WriteLine($"  Docker ({started.Docker.Count}):");
            foreach (var svc in started.Docker)
                _output.WriteLine($"    - {svc.Name}: {svc.Image}");
        }

        return 0;
    }
}
