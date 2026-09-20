using System.Diagnostics;
using System.Text.RegularExpressions;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Processes.Providers;

public sealed partial class DockerProcessProvider : IDockerProcessProvider
{
    private readonly string _auditEndpoint;
    private readonly CapabilityModulesController? _capabilities;

    public DockerProcessProvider(
        ApplicationAuditEndpointProvider? auditEndpoint = null,
        CapabilityModulesController? capabilities = null)
    {
        _auditEndpoint = auditEndpoint?.GetRecordEndpoint() ?? "http://127.0.0.1:5000/api/audit/record";
        _capabilities = capabilities;
    }

    public string GetContainerName(string workspaceId, string appName)
    {
        var safeId = workspaceId[..Math.Min(8, workspaceId.Length)];
        var safeName = ContainerNameSanitizer().Replace(appName.ToLower(), "-").Trim('-');
        return $"agentup-{safeId}-{safeName}";
    }

    public IReadOnlyList<string> CreateRunArguments(string containerName, Workspace workspace, ApplicationInstance app)
    {
        var runtime = _capabilities?.GetRuntime("docker");
        if (runtime is not null && string.Equals(app.CapabilityId, "docker", StringComparison.OrdinalIgnoreCase))
        {
            var host = runtime.Host(CreateHostRequest(containerName, workspace, app));
            if (!host.CanRun)
                throw new InvalidOperationException(host.Messages.Count == 0
                    ? $"Capability 'docker' cannot host '{app.Name}'."
                    : string.Join(" ", host.Messages));
            return host.Arguments;
        }

        var runArgs = new List<string> { "run", "-d", "--name", containerName };
        AddHostGatewayAlias(runArgs);
        AddDockerPortArgs(runArgs, app);
        AddDockerEnvironmentFileArgs(runArgs, app, workspace.WorktreePath);
        AddDockerEnvironmentArgs(runArgs, workspace, app);
        AddDockerVolumeArgs(runArgs, app);
        runArgs.Add(app.Image!);
        if (app.Args is { Count: > 0 })
            runArgs.AddRange(app.Args);
        return runArgs;
    }

    public Process CreateLogProcess(string containerName)
    {
        var logProcess = new Process
        {
            StartInfo = CreateDockerStartInfo("logs", "-f", containerName),
            EnableRaisingEvents = true
        };
        return logProcess;
    }

    public async Task<DockerCommandResult> RunAsync(params string[] args)
    {
        using var process = new Process
        {
            StartInfo = CreateDockerStartInfo(args)
        };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException($"docker could not be started: {ex.Message}", ex);
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new DockerCommandResult(process.ExitCode, stdout, stderr);
    }

    public async Task<int> GetExitCodeAsync(string containerName)
    {
        var result = await RunAsync("inspect", "--format={{.State.ExitCode}}", containerName);
        return int.TryParse(result.Stdout.Trim(), out var code) ? code : 1;
    }

    private ProcessStartInfo CreateDockerStartInfo(params string[] args)
    {
        var wrapped = _capabilities?.WrapModule("docker", "docker", args) ?? new CapabilityLaunchWrapDto("docker", args);
        var startInfo = new ProcessStartInfo
        {
            FileName = wrapped.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in wrapped.Arguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private RuntimeHostRequest CreateHostRequest(string containerName, Workspace workspace, ApplicationInstance app)
    {
        var portVariables = CreateWorkspacePortVariableMap(workspace, app);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in app.Environment ?? new Dictionary<string, string>())
            environment[key] = InterpolateWorkspacePorts(value, portVariables);

        var environmentFiles = (app.EnvironmentFiles ?? [])
            .Select(path => EnvironmentFilePathProvider.ResolveExistingWorkspaceFile(workspace.WorktreePath, path))
            .ToArray();
        var ports = app.AllocatedPorts
            .Select(mapping => new RuntimePortMapping(mapping.Variable, mapping.DefaultPort, mapping.AllocatedPort))
            .ToArray();
        return new RuntimeHostRequest(
            app.Name,
            app.CapabilityVersionRequirement,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["image"] = app.Image ?? "" },
            environment,
            ports,
            app.Volumes ?? [],
            app.Args ?? [],
            workspace.Id,
            containerName,
            environmentFiles,
            _auditEndpoint);
    }

    private static void AddDockerPortArgs(List<string> runArgs, ApplicationInstance app)
    {
        foreach (var mapping in app.AllocatedPorts)
        {
            runArgs.Add("-p");
            runArgs.Add($"{mapping.AllocatedPort}:{mapping.DefaultPort}");
        }
    }

    private static void AddHostGatewayAlias(List<string> runArgs)
    {
        runArgs.Add("--add-host");
        runArgs.Add("host.agent-up:host-gateway");
    }

    private void AddDockerEnvironmentArgs(List<string> runArgs, Workspace workspace, ApplicationInstance app)
    {
        var portVariables = CreateWorkspacePortVariableMap(workspace, app);
        foreach (var (key, value) in app.Environment ?? new Dictionary<string, string>())
            AddEnvironment(runArgs, key, InterpolateWorkspacePorts(value, portVariables));
        AddEnvironment(runArgs, "AGENT_UP_AUDIT_ENDPOINT", GetContainerAuditEndpoint(_auditEndpoint));
        AddEnvironment(runArgs, "AGENT_UP_WORKSPACE_ID", workspace.Id);
        AddEnvironment(runArgs, "AGENT_UP_APPLICATION", app.Name);
    }

    private static void AddEnvironment(List<string> runArgs, string key, string value)
    {
        runArgs.Add("-e");
        runArgs.Add($"{key}={value}");
    }

    private static string GetContainerAuditEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback) return endpoint;
        return new UriBuilder(uri) { Host = "host.agent-up" }.Uri.AbsoluteUri;
    }

    private static Dictionary<string, string> CreateWorkspacePortVariableMap(Workspace workspace, ApplicationInstance app)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var mapping in workspace.Applications.SelectMany(a => a.AllocatedPorts).Where(mapping => mapping.Variable is not null))
            variables[mapping.Variable!] = mapping.AllocatedPort.ToString();

        foreach (var mapping in app.AllocatedPorts.Where(mapping => mapping.Variable is not null))
            variables[mapping.Variable!] = mapping.AllocatedPort.ToString();

        return variables;
    }

    private static string InterpolateWorkspacePorts(string value, IReadOnlyDictionary<string, string> portVariables)
        => PortVariableReference().Replace(value, match =>
            portVariables.TryGetValue(match.Groups["name"].Value, out var port)
                ? port
                : match.Value);

    private static void AddDockerEnvironmentFileArgs(List<string> runArgs, ApplicationInstance app, string worktreePath)
    {
        foreach (var environmentFile in app.EnvironmentFiles ?? [])
        {
            runArgs.Add("--env-file");
            runArgs.Add(EnvironmentFilePathProvider.ResolveExistingWorkspaceFile(worktreePath, environmentFile));
        }
    }

    private static void AddDockerVolumeArgs(List<string> runArgs, ApplicationInstance app)
    {
        foreach (var volume in app.Volumes ?? [])
        {
            runArgs.Add("-v");
            runArgs.Add(volume);
        }
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex ContainerNameSanitizer();

    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex PortVariableReference();
}
