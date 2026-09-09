using AgentUp.CLI.Features.Workspaces.DTOs;

namespace AgentUp.CLI.Features.Workspaces.Services;

public sealed class WorkspaceCommandOutputService
{
    private readonly TextWriter _output;

    public WorkspaceCommandOutputService(TextWriter output)
    {
        _output = output;
    }

    public int WriteListResult(WorkspaceCommandResult<IReadOnlyList<WorkspaceDto>> result)
    {
        if (!result.Succeeded)
        {
            _output.WriteLine(result.Error);
            return 1;
        }

        var workspaces = result.Value!;
        if (workspaces.Count == 0)
        {
            _output.WriteLine("No workspaces registered.");
            return 0;
        }

        _output.WriteLine($"{"ID",-38} {"Name",-20} {"Branch",-24} State");
        _output.WriteLine(new string('-', 90));
        foreach (var workspace in workspaces)
            _output.WriteLine($"{workspace.Id,-38} {workspace.DisplayName,-20} {workspace.Branch,-24} {workspace.State}");

        return 0;
    }

    public int WriteStartResult(WorkspaceCommandResult<StartedWorkspace> result)
    {
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

        WriteSection("Applications", started.Applications, app => $"{app.Name}: {app.Command}");
        WriteSection("Services", started.Services, service => $"{service.Name}: {service.Image}");
        WriteSection(".NET", started.Dotnet, app => $"{app.Name}: dotnet run --project {app.Run.Project}");
        WriteSection("Docker", started.Docker, service => $"{service.Name}: {service.Image}");

        return 0;
    }

    public int WriteStatusResult(WorkspaceResolution resolution)
    {
        if (!resolution.Succeeded)
        {
            _output.WriteLine(resolution.Error);
            return 1;
        }

        var workspace = resolution.Workspace!;
        _output.WriteLine($"Name:       {workspace.DisplayName}");
        _output.WriteLine($"Branch:     {workspace.Branch}");
        _output.WriteLine($"Commit:     {workspace.Commit}");
        _output.WriteLine($"Repository: {workspace.RepositoryPath}");
        _output.WriteLine($"Worktree:   {workspace.WorktreePath}");
        _output.WriteLine($"State:      {workspace.State}");
        return 0;
    }

    public int WriteDiagnosticsResult(WorkspaceCommandResult<WorkspaceDiagnosticsDto> result)
    {
        if (!result.Succeeded)
        {
            _output.WriteLine(result.Error);
            return 1;
        }

        var diagnostics = result.Value!;
        _output.WriteLine($"Diagnostics: {diagnostics.WorkspaceName}");
        _output.WriteLine($"Workspace:   {diagnostics.ProcessState}");
        _output.WriteLine($"Health:      {diagnostics.Health ?? "not configured"}");
        foreach (var app in diagnostics.Applications)
        {
            _output.WriteLine($"Application: {app.Name} ({app.ProcessState}, {app.Health ?? "no health check"})");
            foreach (var line in app.Logs)
                _output.WriteLine($"  log: {line}");
            if (app.LogsTruncated)
                _output.WriteLine("  log: … older lines omitted");
        }

        _output.WriteLine($"Diagnostic entries ({diagnostics.Entries.Count}):");
        foreach (var entry in diagnostics.Entries)
        {
            var context = entry.Application is not null ? $" app={entry.Application}" : string.Empty;
            context += entry.BrowserSession is not null ? $" browser={entry.BrowserSession}" : string.Empty;
            _output.WriteLine($"  [{entry.State}] {entry.Category}/{entry.Severity}{context}: {entry.Message}");
        }
        return 0;
    }

    private void WriteSection<T>(string title, IReadOnlyList<T> items, Func<T, string> format)
    {
        if (items.Count == 0)
            return;

        _output.WriteLine($"  {title} ({items.Count}):");
        foreach (var item in items)
            _output.WriteLine($"    - {format(item)}");
    }
}
