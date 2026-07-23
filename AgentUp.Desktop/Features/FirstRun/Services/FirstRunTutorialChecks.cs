using System.Diagnostics;
using System.Text.Json;
using AgentUp.Desktop.Features.FirstRun.DTOs;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;

namespace AgentUp.Desktop.Features.FirstRun.Services;

public sealed class FirstRunTutorialChecks : IFirstRunTutorialChecks
{
    private static readonly string[] RequiredApplicationNames = ["React SPA", "Express API", "Postgres"];
    private readonly WorkspacesController _workspaces;
    private readonly ProcessRunner _runProcessAsync;

    public FirstRunTutorialChecks(WorkspacesController workspaces)
        : this(workspaces, RunProcessAsync)
    {
    }

    internal FirstRunTutorialChecks(WorkspacesController workspaces, ProcessRunner runProcessAsync)
    {
        _workspaces = workspaces;
        _runProcessAsync = runProcessAsync;
    }

    internal delegate Task<(int ExitCode, string Stdout, string Stderr)> ProcessRunner(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string? workingDirectory = null);

    public async Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _workspaces.CleanupTutorialWorkspacesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Cleanup is best-effort. The regular checks will surface any Server issue later.
            Trace.TraceWarning(ex.Message);
        }
    }

    public async Task<FirstRunCheckResult> CheckDockerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var docker = await _runProcessAsync(
                "docker",
                "--version",
                TimeSpan.FromSeconds(5),
                cancellationToken);
            if (docker.ExitCode != 0)
                return FirstRunCheckResult.Failure(string.IsNullOrWhiteSpace(docker.Stderr)
                    ? "Docker was found but did not report a version. Start Docker Desktop or the Docker daemon, then try again."
                    : docker.Stderr);

            var engine = await _runProcessAsync(
                "docker",
                "info",
                TimeSpan.FromSeconds(8),
                cancellationToken);
            if (engine.ExitCode != 0)
                return FirstRunCheckResult.Failure(string.IsNullOrWhiteSpace(engine.Stderr)
                    ? "Docker is installed, but the engine is not responding. Start Docker Desktop or the Docker daemon, then try again."
                    : engine.Stderr);

            var agentUp = await CheckAgentUpCommandAsync(cancellationToken);
            if (!agentUp.IsSuccess)
                return agentUp;

            var dockerMessage = string.IsNullOrWhiteSpace(docker.Stdout)
                ? "Docker is installed and the engine is responding"
                : $"Docker is installed and the engine is responding ({docker.Stdout})";
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
            var (exitCode, output, error) = await _runProcessAsync(
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

            var npm = await _runProcessAsync("npm", "--version", TimeSpan.FromSeconds(5), cancellationToken);
            if (npm.ExitCode != 0)
                return FirstRunCheckResult.Failure("Node was found, but npm is not available in this desktop session. Restart Agent-Up Desktop and try again.");

            return FirstRunCheckResult.Success($"Node {output.Trim()} and npm {npm.Stdout.Trim()} are available.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return FirstRunCheckResult.Failure("Node was not found. Install Node.js 20 or newer, restart Agent-Up Desktop, then run this check again.");
        }
    }

    public async Task<FirstRunSampleProjectResult> CreateJavaScriptSampleAsync(string? currentProjectDirectory = null, CancellationToken cancellationToken = default)
    {
        var normalized = ResolveAgent1Directory(currentProjectDirectory);

        try
        {
            Directory.CreateDirectory(normalized);
            var webDirectory = Path.Join(normalized, "web");
            var apiDirectory = Path.Join(normalized, "api");
            Directory.CreateDirectory(webDirectory);
            Directory.CreateDirectory(apiDirectory);

            await File.WriteAllTextAsync(Path.Join(webDirectory, "package.json"), WebPackageJson, cancellationToken);
            await File.WriteAllTextAsync(Path.Join(webDirectory, "index.html"), WebIndexHtml, cancellationToken);
            await File.WriteAllTextAsync(Path.Join(webDirectory, "src-App.jsx"), WebAppJsx, cancellationToken);
            await File.WriteAllTextAsync(Path.Join(webDirectory, "vite.config.mjs"), WebViteConfig, cancellationToken);
            await File.WriteAllTextAsync(Path.Join(apiDirectory, "package.json"), ApiPackageJson, cancellationToken);
            await File.WriteAllTextAsync(Path.Join(apiDirectory, "server.js"), ApiServerJs, cancellationToken);
            await File.WriteAllTextAsync(Path.Join(apiDirectory, "products.json"), Agent1ProductsJson, cancellationToken);
            await File.WriteAllTextAsync(Path.Join(normalized, "docker-compose.yaml"), DockerComposeYaml, cancellationToken);

            return FirstRunSampleProjectResult.Success($"Created the sample project at {normalized}.", normalized);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FirstRunSampleProjectResult.Failure($"The sample project could not be created: {ex.Message}");
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
            await File.WriteAllTextAsync(Path.Join(fullPath, "agent-up.json"), AgentUpJson, cancellationToken);
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

        var result = await _runProcessAsync(
            agentUpCommand.FileName,
            $"{agentUpCommand.ArgumentsPrefix} start".Trim(),
            TimeSpan.FromSeconds(30),
            cancellationToken,
            fullPath);

        if (result.ExitCode == 0)
            return FirstRunCheckResult.Success(FormatCommandOutput("agent-up start succeeded. Check the Server registration before continuing.", result));

        return FirstRunCheckResult.Failure(FormatCommandOutput("agent-up start failed.", result));
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

    public async Task<FirstRunCheckResult> CreateDuplicatedJavaScriptSampleAsync(string projectDirectory, CancellationToken cancellationToken = default)
    {
        var agent1Directory = NormalizeProjectDirectory(projectDirectory);
        if (agent1Directory is null)
            return FirstRunCheckResult.Failure("Create the first sample project before duplicating it.");

        if (!Directory.Exists(agent1Directory))
            return FirstRunCheckResult.Failure("The first sample project directory does not exist.");

        var tutorialRoot = Directory.GetParent(agent1Directory)?.FullName;
        if (tutorialRoot is null)
            return FirstRunCheckResult.Failure("Could not infer the tutorial root directory.");

        var agent2Directory = Path.Join(tutorialRoot, "example-agent2");
        try
        {
            if (Directory.Exists(agent2Directory))
                Directory.Delete(agent2Directory, recursive: true);

            CopyDirectory(agent1Directory, agent2Directory);
            await File.WriteAllTextAsync(Path.Join(agent2Directory, "api", "products.json"), Agent2ProductsJson, cancellationToken);
            var agentUpCommand = await ResolveAgentUpCommandAsync(cancellationToken);
            if (agentUpCommand is null)
                return FirstRunCheckResult.Failure("Copied example-agent2, but Agent-Up CLI was not found.");

            var result = await _runProcessAsync(
                agentUpCommand.FileName,
                $"{agentUpCommand.ArgumentsPrefix} start".Trim(),
                TimeSpan.FromSeconds(30),
                cancellationToken,
                agent2Directory);

            return result.ExitCode == 0
                ? FirstRunCheckResult.Success(FormatCommandOutput($"Created and started duplicated workspace at {agent2Directory}.", result))
                : FirstRunCheckResult.Failure(FormatCommandOutput($"Created example-agent2 at {agent2Directory}, but agent-up start failed.", result));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FirstRunCheckResult.Failure($"The duplicate project could not be created: {ex.Message}");
        }
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

        var missingPath = new[]
            {
                Path.Join(fullPath, "web", "package.json"),
                Path.Join(fullPath, "web", "index.html"),
                Path.Join(fullPath, "web", "src-App.jsx"),
                Path.Join(fullPath, "web", "vite.config.mjs"),
                Path.Join(fullPath, "api", "package.json"),
                Path.Join(fullPath, "api", "server.js"),
                Path.Join(fullPath, "docker-compose.yaml")
            }
            .FirstOrDefault(requiredPath => !File.Exists(requiredPath));
        if (missingPath is not null)
            return FirstRunCheckResult.Failure($"Missing sample file: {missingPath}");

        return FirstRunCheckResult.Success("Project files are present.");
    }

    private static FirstRunCheckResult CheckAgentUpJson(string projectDirectory)
    {
        var fullPath = NormalizeProjectDirectory(projectDirectory);
        if (fullPath is null)
            return FirstRunCheckResult.Failure("Choose the sample project directory.");

        if (!Directory.Exists(fullPath))
            return FirstRunCheckResult.Failure("That project directory does not exist yet.");

        var configPath = Path.Join(fullPath, "agent-up.json");
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
            return ((await _workspaces.ListAsync(cancellationToken)).ToList(), null);
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

    private static string ResolveAgent1Directory(string? currentProjectDirectory)
    {
        if (!string.IsNullOrWhiteSpace(currentProjectDirectory))
        {
            var normalized = Path.GetFullPath(Environment.ExpandEnvironmentVariables(currentProjectDirectory.Trim()));
            var parent = Directory.GetParent(normalized);
            if (string.Equals(Path.GetFileName(normalized), "example-agent1", StringComparison.Ordinal)
                && parent is not null
                && string.Equals(Path.GetFileName(parent.FullName), "agent-up-tutorial", StringComparison.Ordinal)
                && !ProjectFilesExist(normalized))
            {
                return normalized;
            }
        }

        return CreateFreshAgent1Directory();
    }

    private static string CreateFreshAgent1Directory()
    {
        while (true)
        {
            var candidate = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "agent-up-tutorial", "example-agent1");
            if (!Directory.Exists(candidate))
                return candidate;
        }
    }

    private static bool ProjectFilesExist(string directory)
        => File.Exists(Path.Join(directory, "docker-compose.yaml"))
           || Directory.Exists(Path.Join(directory, "web"))
           || Directory.Exists(Path.Join(directory, "api"))
           || File.Exists(Path.Join(directory, "agent-up.json"));

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Join(destinationDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var destination = Path.Join(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

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
            try { process.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { Trace.TraceWarning(ex.Message); }
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

    private async Task<AgentUpCommand?> ResolveAgentUpCommandAsync(CancellationToken cancellationToken)
    {
        try
        {
            var installed = await _runProcessAsync("agent-up", "--help", TimeSpan.FromSeconds(5), cancellationToken);
            if (installed.ExitCode == 0)
                return new AgentUpCommand("agent-up", "", false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            Trace.TraceWarning(ex.Message);
        }

        var cliProject = FindCliProject();
        if (cliProject is null)
            return null;

        try
        {
            var fallback = await _runProcessAsync("dotnet", $"run --project \"{cliProject}\" -- --help", TimeSpan.FromSeconds(15), cancellationToken);
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
            var direct = Path.Join(directory.FullName, "AgentUp.CLI.csproj");
            if (File.Exists(direct))
                return direct;

            var candidate = Path.Join(directory.FullName, "AgentUp.CLI", "AgentUp.CLI.csproj");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        return null;
    }

    private static string FormatCommandOutput(string summary, (int ExitCode, string Stdout, string Stderr) result)
    {
        var parts = new List<string>
        {
            summary,
            $"Exit code: {result.ExitCode}"
        };

        if (!string.IsNullOrWhiteSpace(result.Stdout))
            parts.Add($"stdout:{Environment.NewLine}{result.Stdout}");

        if (!string.IsNullOrWhiteSpace(result.Stderr))
            parts.Add($"stderr:{Environment.NewLine}{result.Stderr}");

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var exitTask = process.WaitForExitAsync(cancellationToken);
        var delayTask = Task.Delay(timeout, cancellationToken);
        return await Task.WhenAny(exitTask, delayTask) == exitTask;
    }

    private const string AgentUpJson = """
        {
          "name": "Agent-Up JavaScript Sample",
          "applications": [
            {
              "name": "React SPA",
              "command": "rm -rf node_modules package-lock.json && npm install --package-lock=false && npm run dev",
              "path": "web",
              "ports": [
                { "variable": "WEB_PORT", "defaultPort": 5173, "protocol": "http" }
              ]
            },
            {
              "name": "Express API",
              "command": "rm -rf node_modules package-lock.json && npm install --package-lock=false && npm run dev",
              "path": "api",
              "ports": [
                { "variable": "API_PORT", "defaultPort": 3001, "protocol": "http" }
              ]
            },
            {
              "name": "Postgres",
              "command": "docker compose up database -d && docker compose logs -f database",
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
            "@vitejs/plugin-react": "4.3.4",
            "vite": "5.4.11",
            "react": "18.3.1",
            "react-dom": "18.3.1"
          },
          "devDependencies": {}
        }
        """;

    private const string WebViteConfig = """
        import { defineConfig } from 'vite';
        import react from '@vitejs/plugin-react';

        export default defineConfig({
          plugins: [react()],
          define: {
            __API_PORT__: JSON.stringify(process.env.API_PORT || '3001')
          },
          server: {
            host: '0.0.0.0',
            port: Number(process.env.WEB_PORT || 5173),
            strictPort: true
          }
        });
        """;

    private const string WebIndexHtml = """
        <div id="root"></div>
        <script type="module" src="/src-App.jsx"></script>
        """;

    private const string WebAppJsx = """
        import React, { useEffect, useMemo, useState } from 'react';
        import { createRoot } from 'react-dom/client';

        const apiBaseUrl = `http://localhost:${__API_PORT__}`;

        function formatCurrency(value) {
          return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(Number(value));
        }

        function formatDate(value) {
          return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value));
        }

        function App() {
          const [state, setState] = useState({ status: 'loading', products: [], summary: null, error: null });

          useEffect(() => {
            let cancelled = false;

            async function loadProducts() {
              try {
                const response = await fetch(`${apiBaseUrl}/api/products`);
                const payload = await response.json();
                if (!response.ok) {
                  throw new Error(payload.error || `API returned ${response.status}`);
                }

                if (!cancelled) {
                  setState({ status: 'ready', products: payload.products, summary: payload.summary, error: null });
                }
              } catch (error) {
                if (!cancelled) {
                  setState({ status: 'error', products: [], summary: null, error: error.message });
                }
              }
            }

            loadProducts();
            const interval = window.setInterval(loadProducts, 5000);
            return () => {
              cancelled = true;
              window.clearInterval(interval);
            };
          }, []);

          const summary = useMemo(() => state.summary || {
            productCount: state.products.length,
            totalInventory: 0,
            inventoryValue: 0,
            lowStockCount: 0
          }, [state.summary, state.products.length]);

          return (
            <main className="app-shell">
              <style>{styles}</style>
              <section className="topbar">
                <div>
                  <p className="eyebrow">Agent-Up sample workspace</p>
                  <h1>Product Operations Dashboard</h1>
                  <p className="lede">React is reading live product state from the Express API, and Express is backed by the Postgres container in this workspace.</p>
                </div>
                <div className={`status-pill ${state.status}`}>
                  <span />
                  {state.status === 'ready' ? 'Live data' : state.status === 'loading' ? 'Loading' : 'API attention'}
                </div>
              </section>

              <section className="metrics-grid" aria-label="Inventory summary">
                <Metric label="Products" value={summary.productCount} detail="active catalog rows" />
                <Metric label="Inventory" value={summary.totalInventory.toLocaleString()} detail="units in stock" />
                <Metric label="Stock Value" value={formatCurrency(summary.inventoryValue)} detail="current inventory value" />
                <Metric label="Low Stock" value={summary.lowStockCount} detail="items below reorder line" />
              </section>

              {state.status === 'error' && (
                <section className="notice">
                  <strong>The dashboard could not reach the API.</strong>
                  <span>{state.error}</span>
                </section>
              )}

              <section className="table-panel">
                <div className="table-header">
                  <div>
                    <h2>Product Portfolio</h2>
                    <p>Server-assigned API port: {__API_PORT__}</p>
                  </div>
                  <button type="button" onClick={() => window.location.reload()}>Refresh</button>
                </div>
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>SKU</th>
                        <th>Product</th>
                        <th>Category</th>
                        <th>Status</th>
                        <th className="numeric">Inventory</th>
                        <th className="numeric">Unit Price</th>
                        <th className="numeric">Margin</th>
                        <th>Updated</th>
                      </tr>
                    </thead>
                    <tbody>
                      {state.products.map((product) => (
                        <tr key={product.sku}>
                          <td className="sku">{product.sku}</td>
                          <td>
                            <strong>{product.name}</strong>
                            <span>{product.region}</span>
                          </td>
                          <td>{product.category}</td>
                          <td><span className={`badge ${product.status.toLowerCase().replaceAll(' ', '-')}`}>{product.status}</span></td>
                          <td className="numeric">{Number(product.inventory).toLocaleString()}</td>
                          <td className="numeric">{formatCurrency(product.unit_price)}</td>
                          <td className="numeric">{Number(product.margin).toFixed(1)}%</td>
                          <td>{formatDate(product.updated_at)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>
            </main>
          );
        }

        function Metric({ label, value, detail }) {
          return (
            <article className="metric-card">
              <span>{label}</span>
              <strong>{value}</strong>
              <small>{detail}</small>
            </article>
          );
        }

        const styles = `
          :root {
            color: #18212f;
            background: #eef3f8;
            font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
          }

          * {
            box-sizing: border-box;
          }

          body {
            margin: 0;
          }

          .app-shell {
            min-height: 100vh;
            padding: 32px;
            background:
              linear-gradient(135deg, rgba(13, 92, 117, 0.12), transparent 32%),
              linear-gradient(315deg, rgba(146, 64, 14, 0.08), transparent 28%),
              #eef3f8;
          }

          .topbar {
            display: flex;
            align-items: flex-start;
            justify-content: space-between;
            gap: 24px;
            margin-bottom: 24px;
          }

          .eyebrow {
            margin: 0 0 8px;
            color: #0f766e;
            font-size: 12px;
            font-weight: 800;
            letter-spacing: 0;
            text-transform: uppercase;
          }

          h1, h2, p {
            margin-top: 0;
          }

          h1 {
            margin-bottom: 10px;
            color: #111827;
            font-size: 34px;
            line-height: 1.1;
          }

          .lede {
            max-width: 760px;
            color: #526174;
            font-size: 16px;
            line-height: 1.55;
          }

          .status-pill {
            display: inline-flex;
            align-items: center;
            gap: 10px;
            flex: 0 0 auto;
            padding: 10px 14px;
            border: 1px solid #d5dde8;
            border-radius: 999px;
            background: rgba(255, 255, 255, 0.86);
            color: #334155;
            font-weight: 700;
            box-shadow: 0 10px 28px rgba(31, 41, 55, 0.08);
          }

          .status-pill span {
            width: 9px;
            height: 9px;
            border-radius: 50%;
            background: #f59e0b;
          }

          .status-pill.ready span {
            background: #10b981;
          }

          .status-pill.error span {
            background: #ef4444;
          }

          .metrics-grid {
            display: grid;
            grid-template-columns: repeat(4, minmax(0, 1fr));
            gap: 16px;
            margin-bottom: 24px;
          }

          .metric-card {
            padding: 18px;
            border: 1px solid #dbe3ee;
            border-radius: 8px;
            background: rgba(255, 255, 255, 0.92);
            box-shadow: 0 14px 32px rgba(31, 41, 55, 0.07);
          }

          .metric-card span {
            display: block;
            color: #64748b;
            font-size: 12px;
            font-weight: 800;
            text-transform: uppercase;
          }

          .metric-card strong {
            display: block;
            margin: 10px 0 4px;
            color: #111827;
            font-size: 28px;
          }

          .metric-card small {
            color: #64748b;
          }

          .notice {
            display: flex;
            gap: 12px;
            margin-bottom: 20px;
            padding: 14px 16px;
            border: 1px solid #fecaca;
            border-radius: 8px;
            background: #fff1f2;
            color: #991b1b;
          }

          .table-panel {
            overflow: hidden;
            border: 1px solid #dbe3ee;
            border-radius: 8px;
            background: #ffffff;
            box-shadow: 0 20px 48px rgba(31, 41, 55, 0.1);
          }

          .table-header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 16px;
            padding: 20px 22px;
            border-bottom: 1px solid #e5eaf1;
          }

          .table-header h2 {
            margin-bottom: 4px;
            color: #111827;
            font-size: 20px;
          }

          .table-header p {
            margin-bottom: 0;
            color: #64748b;
          }

          button {
            padding: 10px 14px;
            border: 1px solid #0f766e;
            border-radius: 7px;
            background: #0f766e;
            color: white;
            font-weight: 800;
            cursor: pointer;
          }

          .table-wrap {
            overflow-x: auto;
          }

          table {
            width: 100%;
            min-width: 920px;
            border-collapse: collapse;
          }

          th, td {
            padding: 14px 16px;
            border-bottom: 1px solid #eef2f7;
            text-align: left;
            vertical-align: middle;
            white-space: nowrap;
          }

          th {
            background: #f8fafc;
            color: #475569;
            font-size: 12px;
            font-weight: 800;
            text-transform: uppercase;
          }

          td {
            color: #334155;
          }

          td strong, td span {
            display: block;
          }

          td span {
            margin-top: 3px;
            color: #64748b;
            font-size: 13px;
          }

          .sku {
            color: #0f766e;
            font-weight: 800;
          }

          .numeric {
            text-align: right;
          }

          .badge {
            display: inline-flex;
            width: max-content;
            padding: 5px 9px;
            border-radius: 999px;
            background: #e0f2fe;
            color: #075985;
            font-size: 12px;
            font-weight: 800;
          }

          .badge.at-risk {
            background: #fef3c7;
            color: #92400e;
          }

          .badge.priority {
            background: #dcfce7;
            color: #166534;
          }

          @media (max-width: 860px) {
            .app-shell {
              padding: 20px;
            }

            .topbar {
              flex-direction: column;
            }

            .metrics-grid {
              grid-template-columns: repeat(2, minmax(0, 1fr));
            }
          }

          @media (max-width: 560px) {
            h1 {
              font-size: 28px;
            }

            .metrics-grid {
              grid-template-columns: 1fr;
            }
          }
        `;

        createRoot(document.getElementById('root')).render(<App />);
        """;

    private const string ApiPackageJson = """
        {
          "scripts": {
            "dev": "node server.js"
          },
          "dependencies": {
            "express": "4.18.3",
            "pg": "8.12.0"
          },
          "devDependencies": {}
        }
        """;

    private const string ApiServerJs = """
        const fs = require('fs/promises');
        const path = require('path');
        const express = require('express');
        const { Pool } = require('pg');

        const app = express();
        const port = Number(process.env.API_PORT || 3001);
        const postgresHost = process.env.POSTGRES_HOST || 'localhost';
        const postgresPort = Number(process.env.POSTGRES_PORT || 5432);
        let productsReady = false;
        const pool = new Pool({
          host: postgresHost,
          port: postgresPort,
          user: process.env.POSTGRES_USER || 'postgres',
          password: process.env.POSTGRES_PASSWORD || 'agent-up',
          database: process.env.POSTGRES_DB || 'agentup'
        });

        app.use(express.json());
        app.use((_req, res, next) => {
          res.setHeader('Access-Control-Allow-Origin', '*');
          res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
          next();
        });

        const openApiDocument = {
          openapi: '3.0.3',
          info: {
            title: 'Agent-Up Tutorial Product API',
            version: '1.0.0',
            description: 'A small Express API backed by the tutorial Postgres database.'
          },
          paths: {
            '/health': {
              get: {
                summary: 'Check API and database readiness',
                responses: {
                  '200': { description: 'API and database are ready' },
                  '503': { description: 'Database is not ready yet' }
                }
              }
            },
            '/api/products': {
              get: {
                summary: 'List seeded product rows from Postgres',
                responses: {
                  '200': {
                    description: 'Product data and inventory summary',
                    content: {
                      'application/json': {
                        schema: {
                          type: 'object',
                          properties: {
                            products: {
                              type: 'array',
                              items: { $ref: '#/components/schemas/Product' }
                            },
                            summary: { $ref: '#/components/schemas/ProductSummary' }
                          }
                        }
                      }
                    }
                  },
                  '503': { description: 'Database is not ready yet' }
                }
              }
            }
          },
          components: {
            schemas: {
              Product: {
                type: 'object',
                properties: {
                  sku: { type: 'string' },
                  name: { type: 'string' },
                  category: { type: 'string' },
                  status: { type: 'string' },
                  region: { type: 'string' },
                  inventory: { type: 'integer' },
                  unit_price: { type: 'string' },
                  margin: { type: 'string' },
                  updated_at: { type: 'string', format: 'date-time' }
                }
              },
              ProductSummary: {
                type: 'object',
                properties: {
                  productCount: { type: 'integer' },
                  totalInventory: { type: 'integer' },
                  inventoryValue: { type: 'number' },
                  lowStockCount: { type: 'integer' }
                }
              }
            }
          }
        };

        function openApiExplorerHtml() {
          const documentJson = JSON.stringify(openApiDocument, null, 2).replaceAll('<', '\\u003c');
          return `<!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>Agent-Up Product API</title>
          <style>
            :root { color: #172033; background: #eef3f8; font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
            * { box-sizing: border-box; }
            body { margin: 0; }
            main { min-height: 100vh; padding: 32px; }
            header { display: flex; align-items: flex-start; justify-content: space-between; gap: 24px; margin-bottom: 24px; }
            h1 { margin: 0 0 8px; color: #111827; font-size: 34px; line-height: 1.1; }
            p { margin: 0; color: #526174; line-height: 1.55; }
            .pill { padding: 9px 12px; border-radius: 999px; background: #dcfce7; color: #166534; font-weight: 800; white-space: nowrap; }
            .layout { display: grid; grid-template-columns: minmax(0, 1.1fr) minmax(360px, 0.9fr); gap: 20px; align-items: start; }
            section { border: 1px solid #dbe3ee; border-radius: 8px; background: #fff; box-shadow: 0 18px 44px rgba(31, 41, 55, 0.08); overflow: hidden; }
            .section-head { padding: 18px 20px; border-bottom: 1px solid #e5eaf1; }
            .section-head h2 { margin: 0 0 4px; color: #111827; font-size: 18px; }
            .endpoint { display: grid; grid-template-columns: 84px minmax(0, 1fr) auto; gap: 14px; align-items: center; padding: 16px 20px; border-bottom: 1px solid #eef2f7; }
            .method { width: max-content; padding: 6px 10px; border-radius: 999px; background: #dbeafe; color: #1d4ed8; font-weight: 900; font-size: 12px; }
            code { color: #0f766e; font-weight: 800; }
            button { border: 1px solid #0f766e; border-radius: 7px; background: #0f766e; color: white; padding: 9px 13px; font-weight: 800; cursor: pointer; }
            pre { margin: 0; padding: 18px; overflow: auto; color: #dbeafe; background: #111827; font-size: 13px; line-height: 1.5; min-height: 240px; }
            .spec pre { max-height: 620px; }
            @media (max-width: 900px) { main { padding: 20px; } header, .layout { display: block; } .pill { display: inline-flex; margin-top: 16px; } section { margin-top: 18px; } .endpoint { grid-template-columns: 1fr; } }
          </style>
        </head>
        <body>
          <main>
            <header>
              <div>
                <h1>Product API Explorer</h1>
                <p>OpenAPI-backed Express service for the Agent-Up tutorial. Use the buttons to call the live API that queries Postgres.</p>
              </div>
              <span class="pill">OpenAPI 3.0</span>
            </header>
            <div class="layout">
              <section>
                <div class="section-head">
                  <h2>Endpoints</h2>
                  <p>Responses appear below after each request.</p>
                </div>
                <div class="endpoint">
                  <span class="method">GET</span>
                  <div><code>/health</code><p>Check database readiness.</p></div>
                  <button type="button" data-path="/health">Try it</button>
                </div>
                <div class="endpoint">
                  <span class="method">GET</span>
                  <div><code>/api/products</code><p>Read product rows and summary data from Postgres.</p></div>
                  <button type="button" data-path="/api/products">Try it</button>
                </div>
                <pre id="response">Select an endpoint to run a live request.</pre>
              </section>
              <section class="spec">
                <div class="section-head">
                  <h2>OpenAPI Document</h2>
                  <p>Also available as JSON at <code>/openapi.json</code>.</p>
                </div>
                <pre>${documentJson}</pre>
              </section>
            </div>
          </main>
          <script>
            const output = document.getElementById('response');
            document.querySelectorAll('button[data-path]').forEach((button) => {
              button.addEventListener('click', async () => {
                output.textContent = 'Loading ' + button.dataset.path + ' ...';
                try {
                  const response = await fetch(button.dataset.path);
                  const payload = await response.json();
                  output.textContent = JSON.stringify(payload, null, 2);
                } catch (error) {
                  output.textContent = error.message;
                }
              });
            });
          </script>
        </body>
        </html>`;
        }

        async function readSeedProducts() {
          const raw = await fs.readFile(path.join(__dirname, 'products.json'), 'utf8');
          return JSON.parse(raw);
        }

        async function ensureProducts() {
          await pool.query(`
            create table if not exists products (
              sku text primary key,
              name text not null,
              category text not null,
              status text not null,
              region text not null,
              inventory integer not null,
              unit_price numeric(10, 2) not null,
              margin numeric(5, 2) not null,
              updated_at timestamptz not null
            )
          `);

          const { rows } = await pool.query('select count(*)::int as count from products');
          if (rows[0].count > 0) {
            if (!productsReady) {
              console.log(`Product table ready in Postgres with ${rows[0].count} row(s).`);
              productsReady = true;
            }
            return;
          }

          const products = await readSeedProducts();
          for (const product of products) {
            await pool.query(
              `insert into products (sku, name, category, status, region, inventory, unit_price, margin, updated_at)
               values ($1, $2, $3, $4, $5, $6, $7, $8, $9)`,
              [
                product.sku,
                product.name,
                product.category,
                product.status,
                product.region,
                product.inventory,
                product.unit_price,
                product.margin,
                product.updated_at
              ]
            );
          }
          productsReady = true;
          console.log(`Seeded ${products.length} product row(s) into Postgres.`);
        }

        app.get('/', (_req, res) => {
          res.type('html').send(openApiExplorerHtml());
        });

        app.get('/openapi.json', (_req, res) => {
          res.json(openApiDocument);
        });

        app.get('/health', async (_req, res) => {
          try {
            await ensureProducts();
            res.json({ ok: true, database: 'connected' });
          } catch (error) {
            console.error(`Postgres health check failed: ${error.message}`);
            res.status(503).json({ ok: false, database: error.message });
          }
        });

        app.get('/api/products', async (_req, res) => {
          try {
            await ensureProducts();
            const { rows } = await pool.query(`
              select sku, name, category, status, region, inventory, unit_price, margin, updated_at
              from products
              order by name
            `);

            const summary = rows.reduce((current, product) => {
              const inventory = Number(product.inventory);
              const unitPrice = Number(product.unit_price);
              return {
                productCount: current.productCount + 1,
                totalInventory: current.totalInventory + inventory,
                inventoryValue: current.inventoryValue + inventory * unitPrice,
                lowStockCount: current.lowStockCount + (inventory < 100 ? 1 : 0)
              };
            }, { productCount: 0, totalInventory: 0, inventoryValue: 0, lowStockCount: 0 });

            res.json({ products: rows, summary });
          } catch (error) {
            console.error(`Product query failed: ${error.message}`);
            res.status(503).json({ error: error.message });
          }
        });

        app.listen(port, () => {
          console.log(`Express API listening on ${port}`);
          console.log(`Express API querying Postgres at ${postgresHost}:${postgresPort}`);
        });
        """;

    private const string Agent1ProductsJson = """
        [
          {
            "sku": "SKU-1001",
            "name": "Atlas Analytics Seat",
            "category": "Software",
            "status": "Priority",
            "region": "North America",
            "inventory": 384,
            "unit_price": 129.00,
            "margin": 72.4,
            "updated_at": "2026-07-14T08:30:00Z"
          },
          {
            "sku": "SKU-1002",
            "name": "Beacon Edge Gateway",
            "category": "Hardware",
            "status": "Healthy",
            "region": "Europe",
            "inventory": 146,
            "unit_price": 489.00,
            "margin": 41.2,
            "updated_at": "2026-07-14T09:10:00Z"
          },
          {
            "sku": "SKU-1003",
            "name": "Cedar Support Bundle",
            "category": "Services",
            "status": "Healthy",
            "region": "Global",
            "inventory": 912,
            "unit_price": 79.00,
            "margin": 64.8,
            "updated_at": "2026-07-14T07:45:00Z"
          },
          {
            "sku": "SKU-1004",
            "name": "Delta Sensor Kit",
            "category": "Hardware",
            "status": "At Risk",
            "region": "Asia Pacific",
            "inventory": 58,
            "unit_price": 219.00,
            "margin": 36.6,
            "updated_at": "2026-07-14T06:55:00Z"
          }
        ]
        """;

    private const string Agent2ProductsJson = """
        [
          {
            "sku": "SKU-2001",
            "name": "Atlas Analytics Seat",
            "category": "Software",
            "status": "Healthy",
            "region": "North America",
            "inventory": 412,
            "unit_price": 129.00,
            "margin": 73.1,
            "updated_at": "2026-07-14T08:45:00Z"
          },
          {
            "sku": "SKU-2002",
            "name": "Beacon Edge Gateway",
            "category": "Hardware",
            "status": "At Risk",
            "region": "Europe",
            "inventory": 39,
            "unit_price": 489.00,
            "margin": 39.5,
            "updated_at": "2026-07-14T09:20:00Z"
          },
          {
            "sku": "SKU-2003",
            "name": "Cedar Support Bundle",
            "category": "Services",
            "status": "Healthy",
            "region": "Global",
            "inventory": 864,
            "unit_price": 79.00,
            "margin": 65.0,
            "updated_at": "2026-07-14T07:55:00Z"
          },
          {
            "sku": "SKU-2004",
            "name": "Edison Compliance Pack",
            "category": "Services",
            "status": "Priority",
            "region": "Federal",
            "inventory": 126,
            "unit_price": 349.00,
            "margin": 58.3,
            "updated_at": "2026-07-14T10:05:00Z"
          }
        ]
        """;
}
