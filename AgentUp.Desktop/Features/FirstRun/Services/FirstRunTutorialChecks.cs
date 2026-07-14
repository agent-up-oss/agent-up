using System.Diagnostics;
using System.Text.Json;
using AgentUp.Desktop.Features.Workspaces.Http;

namespace AgentUp.Desktop.Features.FirstRun.Services;

public sealed class FirstRunTutorialChecks(WorkspaceApiClient workspaceClient) : IFirstRunTutorialChecks
{
    private static readonly string[] RequiredApplicationNames = ["React SPA", "Express API", "Postgres"];

    public async Task<FirstRunCheckResult> CheckDockerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var docker = await RunProcessAsync(
                "docker",
                "version --format {{.Server.Version}}",
                TimeSpan.FromSeconds(8),
                cancellationToken);
            if (docker.ExitCode != 0)
                return FirstRunCheckResult.Failure(string.IsNullOrWhiteSpace(docker.Stderr)
                    ? "Docker is not responding. Start Docker Desktop or the Docker daemon, then try again."
                    : docker.Stderr);

            var agentUp = await CheckAgentUpCommandAsync(cancellationToken);
            if (!agentUp.IsSuccess)
                return agentUp;

            var dockerMessage = string.IsNullOrWhiteSpace(docker.Stdout)
                ? "Docker is installed and responding"
                : $"Docker is installed and responding ({docker.Stdout})";
            return FirstRunCheckResult.Success($"{dockerMessage}. {agentUp.Message}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return FirstRunCheckResult.Failure("Docker was not found. Install Docker, then run this check again.");
        }
    }

    public async Task<FirstRunCheckResult> CheckNodeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (exitCode, output, error) = await RunProcessAsync(
                "node",
                "--version",
                TimeSpan.FromSeconds(5),
                cancellationToken);
            if (exitCode != 0)
                return FirstRunCheckResult.Failure(string.IsNullOrWhiteSpace(error)
                    ? "Node is not responding. Install Node.js 20 or newer, then restart Agent-Up Desktop."
                    : error);

            var versionText = output.Trim().TrimStart('v', 'V');
            var majorText = versionText.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!int.TryParse(majorText, out var major) || major < 20)
                return FirstRunCheckResult.Failure($"Node {output.Trim()} was found. Install Node.js 20 or newer, then restart Agent-Up Desktop.");

            var npm = await RunProcessAsync("npm", "--version", TimeSpan.FromSeconds(5), cancellationToken);
            if (npm.ExitCode != 0)
                return FirstRunCheckResult.Failure("Node was found, but npm is not available in this desktop session. Restart Agent-Up Desktop and try again.");

            return FirstRunCheckResult.Success($"Node {output.Trim()} and npm {npm.Stdout.Trim()} are available.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return FirstRunCheckResult.Failure("Node was not found. Install Node.js 20 or newer, restart Agent-Up Desktop, then run this check again.");
        }
    }

    public async Task<FirstRunCheckResult> CreateJavaScriptSampleAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProjectDirectory(projectDirectory);
        if (normalized is null)
            return FirstRunCheckResult.Failure("Choose a folder where Agent-Up can create the JavaScript sample project.");

        try
        {
            Directory.CreateDirectory(normalized);
            var webDirectory = Path.Combine(normalized, "web");
            var apiDirectory = Path.Combine(normalized, "api");
            Directory.CreateDirectory(webDirectory);
            Directory.CreateDirectory(apiDirectory);

            await File.WriteAllTextAsync(Path.Combine(webDirectory, "package.json"), WebPackageJson, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(webDirectory, "index.html"), WebIndexHtml, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(webDirectory, "src-App.jsx"), WebAppJsx, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(apiDirectory, "package.json"), ApiPackageJson, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(apiDirectory, "server.js"), ApiServerJs, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(normalized, "docker-compose.yaml"), DockerComposeYaml, cancellationToken);

            return FirstRunCheckResult.Success("Created the React SPA, Express API, and docker-compose.yaml files.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FirstRunCheckResult.Failure($"The sample project could not be created: {ex.Message}");
        }
    }

    public Task<FirstRunCheckResult> CheckJavaScriptProjectFilesAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var projectCheck = CheckProjectFiles(projectDirectory);
        return Task.FromResult(projectCheck);
    }

    public async Task<FirstRunCheckResult> CreateAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var fullPath = NormalizeProjectDirectory(projectDirectory);
        if (fullPath is null)
            return FirstRunCheckResult.Failure("Choose the sample project directory first.");

        if (!Directory.Exists(fullPath))
            return FirstRunCheckResult.Failure("That project directory does not exist yet.");

        try
        {
            await File.WriteAllTextAsync(Path.Combine(fullPath, "agent-up.json"), AgentUpJson, cancellationToken);
            return FirstRunCheckResult.Success("Created agent-up.json in the sample project directory.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FirstRunCheckResult.Failure($"agent-up.json could not be created: {ex.Message}");
        }
    }

    public Task<FirstRunCheckResult> CheckAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default)
        => Task.FromResult(CheckAgentUpJson(projectDirectory));

    public async Task<FirstRunCheckResult> StartJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var fullPath = NormalizeProjectDirectory(projectDirectory);
        if (fullPath is null)
            return FirstRunCheckResult.Failure("Choose the sample project directory first.");

        if (!Directory.Exists(fullPath))
            return FirstRunCheckResult.Failure("That project directory does not exist yet.");

        var agentUpCommand = await ResolveAgentUpCommandAsync(cancellationToken);
        if (agentUpCommand is null)
            return FirstRunCheckResult.Failure("Agent-Up CLI was not found. Install `agent-up` or run from a checkout that contains AgentUp.CLI.csproj.");

        var result = await RunProcessAsync(
            agentUpCommand.FileName,
            $"{agentUpCommand.ArgumentsPrefix} start".Trim(),
            TimeSpan.FromSeconds(30),
            cancellationToken,
            fullPath);

        if (result.ExitCode == 0)
            return FirstRunCheckResult.Success("Started the sample workspace. Check the Server registration before continuing.");

        return FirstRunCheckResult.Failure(string.IsNullOrWhiteSpace(result.Stderr)
            ? "agent-up start failed."
            : result.Stderr);
    }

    public async Task<FirstRunCheckResult> CheckJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var projectCheck = CheckProjectFiles(projectDirectory);
        if (!projectCheck.IsSuccess)
            return projectCheck;

        var agentUpJsonCheck = CheckAgentUpJson(projectDirectory);
        if (!agentUpJsonCheck.IsSuccess)
            return agentUpJsonCheck;

        var normalized = NormalizeProjectDirectory(projectDirectory)!;
        var (workspaces, listError) = await ListWorkspacesAsync(cancellationToken);
        if (listError is not null)
            return FirstRunCheckResult.Failure(listError);

        var workspace = workspaces.FirstOrDefault(w =>
            string.Equals(Path.GetFullPath(w.RepositoryPath), normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFullPath(w.WorktreePath), normalized, StringComparison.OrdinalIgnoreCase));

        if (workspace is null)
            return FirstRunCheckResult.Failure("The sample workspace has not appeared on the Server yet. Run `agent-up start` in that directory, then check again.");

        var missing = RequiredApplicationNames
            .Where(name => workspace.Applications.All(app => !string.Equals(app.Name, name, StringComparison.Ordinal)))
            .ToList();
        if (missing.Count > 0)
            return FirstRunCheckResult.Failure($"The workspace is missing: {string.Join(", ", missing)}.");

        return FirstRunCheckResult.Success("The sample workspace is registered and all three components are present.");
    }

    public async Task<FirstRunCheckResult> CheckDuplicatedJavaScriptWorkspacesAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProjectDirectory(projectDirectory);
        if (normalized is null)
            return FirstRunCheckResult.Failure("Choose the original sample project folder first.");

        var (workspaces, listError) = await ListWorkspacesAsync(cancellationToken);
        if (listError is not null)
            return FirstRunCheckResult.Failure(listError);

        var matching = workspaces
            .Where(w => RequiredApplicationNames.All(name => w.Applications.Any(app => string.Equals(app.Name, name, StringComparison.Ordinal))))
            .ToList();

        if (matching.Count < 2)
            return FirstRunCheckResult.Failure("Only one matching sample workspace is registered. Duplicate the directory and run `agent-up start` in the duplicate, then check again.");

        var allocatedPorts = matching
            .SelectMany(w => w.Applications.SelectMany(app => app.AllocatedPorts))
            .Where(port => port.AllocatedPort > 0)
            .Select(port => port.AllocatedPort)
            .ToList();

        if (allocatedPorts.Count != allocatedPorts.Distinct().Count())
            return FirstRunCheckResult.Failure("The duplicated workspaces are registered, but at least one allocated port collides.");

        return FirstRunCheckResult.Success("Two matching workspaces are registered and all allocated ports are unique.");
    }

    private static FirstRunCheckResult CheckProjectFiles(string projectDirectory)
    {
        var fullPath = NormalizeProjectDirectory(projectDirectory);
        if (fullPath is null)
            return FirstRunCheckResult.Failure("Choose the sample project directory.");

        if (!Directory.Exists(fullPath))
            return FirstRunCheckResult.Failure("That project directory does not exist yet.");

        foreach (var requiredPath in new[]
                 {
                     Path.Combine(fullPath, "web", "package.json"),
                     Path.Combine(fullPath, "web", "index.html"),
                     Path.Combine(fullPath, "web", "src-App.jsx"),
                     Path.Combine(fullPath, "api", "package.json"),
                     Path.Combine(fullPath, "api", "server.js"),
                     Path.Combine(fullPath, "docker-compose.yaml")
                 })
        {
            if (!File.Exists(requiredPath))
                return FirstRunCheckResult.Failure($"Missing sample file: {requiredPath}");
        }

        return FirstRunCheckResult.Success("Project files are present.");
    }

    private static FirstRunCheckResult CheckAgentUpJson(string projectDirectory)
    {
        var fullPath = NormalizeProjectDirectory(projectDirectory);
        if (fullPath is null)
            return FirstRunCheckResult.Failure("Choose the sample project directory.");

        if (!Directory.Exists(fullPath))
            return FirstRunCheckResult.Failure("That project directory does not exist yet.");

        var configPath = Path.Combine(fullPath, "agent-up.json");
        if (!File.Exists(configPath))
            return FirstRunCheckResult.Failure("No agent-up.json was found in the sample project directory.");

        try
        {
            using var stream = File.OpenRead(configPath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("applications", out var applications)
                || applications.ValueKind != JsonValueKind.Array
                || applications.GetArrayLength() == 0)
            {
                return FirstRunCheckResult.Failure("agent-up.json must contain at least one application.");
            }

            return FirstRunCheckResult.Success("Project files and agent-up.json are present.");
        }
        catch (JsonException ex)
        {
            return FirstRunCheckResult.Failure($"agent-up.json is not valid JSON: {ex.Message}");
        }
        catch (IOException ex)
        {
            return FirstRunCheckResult.Failure($"agent-up.json could not be read: {ex.Message}");
        }
    }

    private async Task<(List<WorkspaceDto> Workspaces, string? Error)> ListWorkspacesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await workspaceClient.ListAsync(cancellationToken), null);
        }
        catch (HttpRequestException ex)
        {
            return ([], $"The Server could not be reached: {ex.Message}");
        }
    }

    private static string? NormalizeProjectDirectory(string projectDirectory)
        => string.IsNullOrWhiteSpace(projectDirectory)
            ? null
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(projectDirectory.Trim()));

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };

        using var process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException($"{fileName} could not be started.");

        var exited = await WaitForExitAsync(process, timeout, cancellationToken);
        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return (-1, "", $"{fileName} did not respond in time.");
        }

        var output = (await process.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
        var error = (await process.StandardError.ReadToEndAsync(cancellationToken)).Trim();
        return (process.ExitCode, output, error);
    }

    private async Task<FirstRunCheckResult> CheckAgentUpCommandAsync(CancellationToken cancellationToken)
    {
        var command = await ResolveAgentUpCommandAsync(cancellationToken);
        if (command is null)
            return FirstRunCheckResult.Failure("Docker works, but Agent-Up CLI was not found. Install `agent-up` or run from a checkout that contains AgentUp.CLI.csproj.");

        return FirstRunCheckResult.Success(command.IsFallback
            ? "Agent-Up CLI is available through the local AgentUp.CLI project fallback."
            : "Agent-Up CLI is available on PATH.");
    }

    private static async Task<AgentUpCommand?> ResolveAgentUpCommandAsync(CancellationToken cancellationToken)
    {
        try
        {
            var installed = await RunProcessAsync("agent-up", "--help", TimeSpan.FromSeconds(5), cancellationToken);
            if (installed.ExitCode == 0)
                return new AgentUpCommand("agent-up", "", false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
        }

        var cliProject = FindCliProject();
        if (cliProject is null)
            return null;

        try
        {
            var fallback = await RunProcessAsync("dotnet", $"run --project \"{cliProject}\" -- --help", TimeSpan.FromSeconds(15), cancellationToken);
            return fallback.ExitCode == 0
                ? new AgentUpCommand("dotnet", $"run --project \"{cliProject}\" --", true)
                : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return null;
        }
    }

    private static string? FindCliProject()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "AgentUp.CLI", "AgentUp.CLI.csproj");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        return null;
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var exitTask = process.WaitForExitAsync(cancellationToken);
        var delayTask = Task.Delay(timeout, cancellationToken);
        return await Task.WhenAny(exitTask, delayTask) == exitTask;
    }

    private sealed record AgentUpCommand(string FileName, string ArgumentsPrefix, bool IsFallback);

    private const string AgentUpJson = """
        {
          "name": "Agent-Up JavaScript Sample",
          "applications": [
            {
              "name": "React SPA",
              "command": "npm install && npm run dev",
              "path": "web",
              "ports": [
                { "variable": "WEB_PORT", "defaultPort": 5173, "protocol": "http" }
              ]
            },
            {
              "name": "Express API",
              "command": "npm install && npm run dev",
              "path": "api",
              "ports": [
                { "variable": "API_PORT", "defaultPort": 3001, "protocol": "http" }
              ]
            },
            {
              "name": "Postgres",
              "command": "docker compose up database -d",
              "ports": [
                { "variable": "POSTGRES_PORT", "defaultPort": 5432, "protocol": "tcp" }
              ]
            }
          ]
        }
        """;

    private const string DockerComposeYaml = """
        services:
          database:
            image: postgres:16
            environment:
              POSTGRES_USER: postgres
              POSTGRES_PASSWORD: agent-up
              POSTGRES_DB: agentup
            ports:
              - "${POSTGRES_PORT:-5432}:5432"
            volumes:
              - pgdata:/var/lib/postgresql/data

        volumes:
          pgdata:
        """;

    private const string WebPackageJson = """
        {
          "scripts": {
            "dev": "vite --host 0.0.0.0"
          },
          "dependencies": {
            "@vitejs/plugin-react": "latest",
            "vite": "latest",
            "react": "latest",
            "react-dom": "latest"
          },
          "devDependencies": {}
        }
        """;

    private const string WebIndexHtml = """
        <div id="root"></div>
        <script type="module" src="/src-App.jsx"></script>
        """;

    private const string WebAppJsx = """
        import React from 'react';
        import { createRoot } from 'react-dom/client';

        function App() {
          return (
            <main style={{ fontFamily: 'system-ui', padding: 32 }}>
              <h1>Agent-Up React sample</h1>
              <p>The SPA is running on the Server-assigned WEB_PORT.</p>
            </main>
          );
        }

        createRoot(document.getElementById('root')).render(<App />);
        """;

    private const string ApiPackageJson = """
        {
          "scripts": {
            "dev": "node server.js"
          },
          "dependencies": {
            "express": "latest",
            "pg": "latest"
          },
          "devDependencies": {}
        }
        """;

    private const string ApiServerJs = """
        const express = require('express');
        const { Pool } = require('pg');

        const app = express();
        const port = Number(process.env.API_PORT || 3001);
        const pool = new Pool({
          host: process.env.POSTGRES_HOST || 'localhost',
          port: Number(process.env.POSTGRES_PORT || 5432),
          user: process.env.POSTGRES_USER || 'postgres',
          password: process.env.POSTGRES_PASSWORD || 'agent-up',
          database: process.env.POSTGRES_DB || 'agentup'
        });

        app.get('/health', async (_req, res) => {
          try {
            await pool.query('select 1');
            res.json({ ok: true, database: 'connected' });
          } catch (error) {
            res.json({ ok: true, database: error.message });
          }
        });

        app.listen(port, () => {
          console.log(`Express API listening on ${port}`);
        });
        """;
}
