using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;

public sealed class CapabilityLifecycleSmoke
{
    private const string WorkspaceName = "Capability Lifecycle Smoke Workspace";
    private const string DotnetAppName = "SmokeDotnet";
    private const string DockerAppName = "SmokeDocker";
    private const string WorkingDirectoryEnvironmentKey = "AGENTUP_SMOKE_WORKING_DIRECTORY";

    private readonly ICommandRunner _commands;
    private readonly HttpClient _http;

    public CapabilityLifecycleSmoke(ICommandRunner commands, HttpClient? http = null)
    {
        _commands = commands;
        _http = http ?? new HttpClient();
    }

    public async Task RunAsync(
        string workDirectory,
        InstalledServiceContext context,
        string serverUrl,
        FileAssertions assert,
        CancellationToken cancellationToken)
    {
        var repo = Path.Join(workDirectory, "capability-workspace");
        PrepareWorkspace(repo);
        await GitCommitConfigAsync(repo, assert, cancellationToken);

        var environment = MergeEnvironment(context.CliEnvironment, "AGENTUP_SERVER_URL", serverUrl);
        var start = await _commands.RunAsync(CliCommand(context.CliCommand, "start", repo, environment), cancellationToken);
        await File.WriteAllTextAsync(Path.Join(workDirectory, "capability-cli-start.log"), start.Stdout + start.Stderr, cancellationToken);
        if (start.ExitCode != 0 || !start.Stdout.Contains($"Started workspace \"{WorkspaceName}\"", StringComparison.Ordinal))
        {
            assert.Error("capability.cli.start", $"Capability workspace start failed: {start.Stderr}{start.Stdout}");
            return;
        }

        var workspace = await FindWorkspaceAsync(serverUrl, cancellationToken);
        if (workspace is null)
        {
            assert.Error("capability.workspace.find", "Capability smoke workspace was not returned by the Server.");
            return;
        }

        AssertApplication(workspace.Value, DotnetAppName, "dotnet", assert);
        AssertApplication(workspace.Value, DockerAppName, "docker", assert);
        await AssertStateAsync(serverUrl, workspace.Value.Id, DotnetAppName, "Running", assert, cancellationToken);
        await AssertStateAsync(serverUrl, workspace.Value.Id, DockerAppName, "Running", assert, cancellationToken);

        await StopApplicationAsync(serverUrl, workspace.Value.Id, DotnetAppName, assert, cancellationToken);
        await AssertStateAsync(serverUrl, workspace.Value.Id, DotnetAppName, "Stopped", assert, cancellationToken);
        await AssertStateAsync(serverUrl, workspace.Value.Id, DockerAppName, "Running", assert, cancellationToken);

        await StartApplicationAsync(serverUrl, workspace.Value.Id, DotnetAppName, assert, cancellationToken);
        await AssertStateAsync(serverUrl, workspace.Value.Id, DotnetAppName, "Running", assert, cancellationToken);

        await StopApplicationAsync(serverUrl, workspace.Value.Id, DockerAppName, assert, cancellationToken);
        await AssertStateAsync(serverUrl, workspace.Value.Id, DockerAppName, "Stopped", assert, cancellationToken);

        await StopWorkspaceAsync(serverUrl, workspace.Value.Id, assert, cancellationToken);
        await AssertStateAsync(serverUrl, workspace.Value.Id, DotnetAppName, "Stopped", assert, cancellationToken);
        await AssertStateAsync(serverUrl, workspace.Value.Id, DockerAppName, "Stopped", assert, cancellationToken);
    }

    private static void PrepareWorkspace(string repo)
    {
        Directory.CreateDirectory(Path.Join(repo, "SmokeDotnet"));
        File.WriteAllText(Path.Join(repo, "SmokeDotnet", "SmokeDotnet.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Join(repo, "SmokeDotnet", "Program.cs"), """
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.MapGet("/", () => "dotnet capability smoke");
            var port = Environment.GetEnvironmentVariable("WEB_PORT") ?? "5000";
            app.Run($"http://127.0.0.1:{port}");
            """);
        File.WriteAllText(Path.Join(repo, "agent-up.json"), $$"""
            {
              "name": "Capability Lifecycle Smoke Workspace",
              "dotnet": [
                {
                  "name": "SmokeDotnet",
                  "sdk": "10.0.x",
                  "run": {
                    "project": "SmokeDotnet/SmokeDotnet.csproj"
                  },
                  "ports": [{ "variable": "WEB_PORT", "defaultPort": 5000, "protocol": "http" }]
                }
              ],
              "docker": [
                {
                  "name": "SmokeDocker",
                  "image": "{{DockerSmokeImage()}}",
                  "ports": [{ "variable": "DOCKER_WEB_PORT", "defaultPort": 80, "protocol": "http" }]
                }
              ]
            }
            """);
    }

    private static string DockerSmokeImage()
    {
        var image = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_DOCKER_IMAGE");
        if (!string.IsNullOrWhiteSpace(image))
            return image;

        if (!OperatingSystem.IsWindows())
            return "nginx:alpine";

        var tag = Environment.OSVersion.Version.Build >= 26000
            ? "windowsservercore-ltsc2025"
            : "windowsservercore-ltsc2022";
        return $"mcr.microsoft.com/windows/servercore/iis:{tag}";
    }

    private async Task GitCommitConfigAsync(string repo, FileAssertions assert, CancellationToken cancellationToken)
    {
        foreach (var command in GitCommands(repo))
        {
            var result = await _commands.RunAsync(command.Spec, cancellationToken);
            if (result.ExitCode != 0)
                assert.Error(command.Code, $"{command.Spec.FileName} failed: {result.Stderr}{result.Stdout}");
        }
    }

    private async Task<WorkspaceSnapshot?> FindWorkspaceAsync(string serverUrl, CancellationToken cancellationToken)
    {
        var workspaces = await _http.GetFromJsonAsync<JsonElement[]>(Endpoint(serverUrl, "/api/workspaces"), cancellationToken);
        if (workspaces is null)
            return null;

        var matchingWorkspaces = workspaces.Where(workspace =>
            TryGetString(workspace, "displayName", out var name) && name == WorkspaceName);
        foreach (var workspace in matchingWorkspaces)
        {
            if (!TryGetString(workspace, "id", out var id))
                return null;
            return new WorkspaceSnapshot(id, workspace);
        }

        return null;
    }

    private static void AssertApplication(WorkspaceSnapshot workspace, string appName, string capabilityId, FileAssertions assert)
    {
        var app = FindApplication(workspace.Json, appName);
        if (app is null)
        {
            assert.Error($"capability.{capabilityId}.registered", $"{appName} was not registered.");
            return;
        }

        if (!TryGetString(app.Value, "capabilityId", out var actualCapability) || actualCapability != capabilityId)
            assert.Error($"capability.{capabilityId}.id", $"{appName} did not report capability id '{capabilityId}'.");

        if (app.Value.TryGetProperty("capabilityStatus", out var status)
            && status.TryGetProperty("canRun", out var canRun)
            && canRun.ValueKind == JsonValueKind.False)
            assert.Error($"capability.{capabilityId}.canRun", $"{appName} capability status cannot run.");
    }

    private async Task AssertStateAsync(
        string serverUrl,
        string workspaceId,
        string appName,
        string expected,
        FileAssertions assert,
        CancellationToken cancellationToken)
    {
        var app = await GetApplicationAsync(serverUrl, workspaceId, appName, cancellationToken);
        var actual = app is null ? "<missing>" : ReadState(app.Value);
        if (actual != expected)
            assert.Error($"capability.{appName.ToLowerInvariant()}.state", $"{appName} expected {expected}, got {actual}.");
    }

    private async Task StartApplicationAsync(string serverUrl, string workspaceId, string appName, FileAssertions assert, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync(Endpoint(serverUrl, $"/api/workspaces/{workspaceId}/applications/{Uri.EscapeDataString(appName)}/start"), null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            assert.Error($"capability.{appName.ToLowerInvariant()}.start", $"{appName} start failed with HTTP {(int)response.StatusCode}.");
    }

    private async Task StopApplicationAsync(string serverUrl, string workspaceId, string appName, FileAssertions assert, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync(Endpoint(serverUrl, $"/api/workspaces/{workspaceId}/applications/{Uri.EscapeDataString(appName)}/stop"), null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            assert.Error($"capability.{appName.ToLowerInvariant()}.stop", $"{appName} stop failed with HTTP {(int)response.StatusCode}.");
    }

    private async Task StopWorkspaceAsync(string serverUrl, string workspaceId, FileAssertions assert, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync(Endpoint(serverUrl, $"/api/workspaces/{workspaceId}/stop"), null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            assert.Error("capability.workspace.stop", $"Workspace stop failed with HTTP {(int)response.StatusCode}.");
    }

    private async Task<JsonElement?> GetApplicationAsync(string serverUrl, string workspaceId, string appName, CancellationToken cancellationToken)
    {
        var workspace = await _http.GetFromJsonAsync<JsonElement>(Endpoint(serverUrl, $"/api/workspaces/{workspaceId}"), cancellationToken);
        return FindApplication(workspace, appName);
    }

    private static JsonElement? FindApplication(JsonElement workspace, string appName)
    {
        if (!workspace.TryGetProperty("applications", out var apps) || apps.ValueKind != JsonValueKind.Array)
            return null;

        return apps.EnumerateArray()
            .Where(app => TryGetString(app, "name", out var name) && name == appName)
            .Select<JsonElement, JsonElement?>(app => app)
            .FirstOrDefault();
    }

    private static string ReadState(JsonElement app)
    {
        if (!app.TryGetProperty("state", out var state))
            return "<missing>";
        if (state.ValueKind == JsonValueKind.String)
            return state.GetString() ?? "<missing>";
        if (state.ValueKind == JsonValueKind.Number && state.TryGetInt32(out var value))
            return value switch
            {
                0 => "Stopped",
                1 => "Starting",
                2 => "Running",
                3 => "Stopping",
                4 => "Failed",
                _ => value.ToString()
            };
        return state.ToString();
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = "";
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }

    private static IReadOnlyList<(CommandSpec Spec, string Code)> GitCommands(string repo)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkingDirectoryEnvironmentKey] = repo
        };

        return OperatingSystem.IsWindows()
            ?
            [
                (new CommandSpec("powershell.exe", ["-NoProfile", "-Command", "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git init -q"], Environment: environment), "capability.git.init"),
                (new CommandSpec("powershell.exe", ["-NoProfile", "-Command", "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git config user.email ci@agent-up.local"], Environment: environment), "capability.git.email"),
                (new CommandSpec("powershell.exe", ["-NoProfile", "-Command", "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git config user.name \"Agent-Up CI\""], Environment: environment), "capability.git.name"),
                (new CommandSpec("powershell.exe", ["-NoProfile", "-Command", "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git add agent-up.json"], Environment: environment), "capability.git.add"),
                (new CommandSpec("powershell.exe", ["-NoProfile", "-Command", "Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; git commit -q -m \"Add service smoke workspace\""], Environment: environment), "capability.git.commit")
            ]
            :
            [
                (new CommandSpec("bash", ["-lc", "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git init -q"], Environment: environment), "capability.git.init"),
                (new CommandSpec("bash", ["-lc", "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git config user.email ci@agent-up.local"], Environment: environment), "capability.git.email"),
                (new CommandSpec("bash", ["-lc", "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git config user.name \"Agent-Up CI\""], Environment: environment), "capability.git.name"),
                (new CommandSpec("bash", ["-lc", "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git add agent-up.json"], Environment: environment), "capability.git.add"),
                (new CommandSpec("bash", ["-lc", "cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && git commit -q -m \"Add service smoke workspace\""], Environment: environment), "capability.git.commit")
            ];
    }

    private static CommandSpec CliCommand(
        string command,
        string argument,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment)
    {
        var workingEnvironment = MergeEnvironment(environment, WorkingDirectoryEnvironmentKey, workingDirectory);
        return command == "cmd.exe"
            ? new CommandSpec("powershell.exe", ["-NoProfile", "-Command", $"Set-Location -LiteralPath $env:AGENTUP_SMOKE_WORKING_DIRECTORY; agent-up.cmd {argument}"], Environment: workingEnvironment)
            : new CommandSpec("bash", ["-lc", $"cd \"$AGENTUP_SMOKE_WORKING_DIRECTORY\" && agent-up {argument}"], Environment: workingEnvironment);
    }

    private static Dictionary<string, string> MergeEnvironment(
        IReadOnlyDictionary<string, string>? source,
        string key,
        string value)
    {
        var environment = source is null
            ? []
            : new Dictionary<string, string>(source, StringComparer.Ordinal);
        environment[key] = value;
        return environment;
    }

    private static string Endpoint(string serverUrl, string path)
        => serverUrl.TrimEnd('/') + path;

    private readonly record struct WorkspaceSnapshot(string Id, JsonElement Json);
}
