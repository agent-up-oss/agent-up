using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Processes.Providers;

public sealed partial class LocalProcessProvider : ILocalProcessProvider
{
    private readonly string _auditEndpoint;

    public LocalProcessProvider(ApplicationAuditEndpointProvider? auditEndpoint = null)
    {
        _auditEndpoint = auditEndpoint?.GetRecordEndpoint() ?? "http://127.0.0.1:5000/api/audit/record";
    }

    public Process CreateApplicationProcess(Workspace workspace, ApplicationInstance app)
        => new()
        {
            StartInfo = CreateStartInfo(workspace, app),
            EnableRaisingEvents = true
        };

    public void Kill(Process process)
        => process.Kill(entireProcessTree: true);

    internal ProcessStartInfo CreateStartInfo(Workspace workspace, ApplicationInstance app)
    {
        var workingDirectory = WorkspacePathProvider.ResolveWorkspacePath(
            workspace.WorktreePath,
            app.Path,
            "Application path");
        var fileEnvironment = LoadEnvironmentFiles(workspace.WorktreePath, app.EnvironmentFiles);
        var startInfo = CreateProcessStartInfo(app.Command!, workingDirectory);
        foreach (var (key, value) in fileEnvironment)
            startInfo.Environment[key] = value;

        foreach (var (key, value) in app.Environment ?? new Dictionary<string, string>())
            startInfo.Environment[key] = value;

        foreach (var mapping in workspace.Applications.SelectMany(a => a.AllocatedPorts).Where(mapping => mapping.Variable is not null))
            startInfo.Environment[mapping.Variable!] = mapping.AllocatedPort.ToString();

        startInfo.Environment["AGENT_UP_AUDIT_ENDPOINT"] = _auditEndpoint;
        startInfo.Environment["AGENT_UP_WORKSPACE_ID"] = workspace.Id;
        startInfo.Environment["AGENT_UP_APPLICATION"] = app.Name;

        return startInfo;
    }

    private static Dictionary<string, string> LoadEnvironmentFiles(string worktreePath, IReadOnlyList<string>? environmentFiles)
    {
        var environment = new Dictionary<string, string>();
        foreach (var environmentFile in environmentFiles ?? [])
        {
            var lines = EnvironmentFilePathProvider.ReadExistingWorkspaceFileLines(worktreePath, environmentFile);
            foreach (var (key, value) in ParseEnvironmentFile(lines, environmentFile))
                environment[key] = value;
        }

        return environment;
    }

    internal static Dictionary<string, string> ParseEnvironmentFile(IEnumerable<string> lines, string sourceName)
    {
        var environment = new Dictionary<string, string>();
        var lineNumber = 0;
        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line["export ".Length..].TrimStart();

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                throw new InvalidOperationException($"Environment file '{sourceName}' has an invalid entry on line {lineNumber}.");

            var key = line[..equalsIndex].Trim();
            if (!EnvironmentVariableName().IsMatch(key))
                throw new InvalidOperationException($"Environment file '{sourceName}' has an invalid variable name on line {lineNumber}.");

            var value = line[(equalsIndex + 1)..].Trim();
            if (value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            environment[key] = value;
        }

        return environment;
    }

    private static ProcessStartInfo CreateProcessStartInfo(string command, string workingDirectory)
    {
        var parsed = ParseApplicationCommand(command);
        var startInfo = new ProcessStartInfo
        {
            FileName = parsed.FileName,
            WorkingDirectory = TrustedProcessWorkingDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in ApplyWorkingDirectoryArguments(parsed.FileName, parsed.Arguments, workingDirectory))
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }

    private static (string FileName, IReadOnlyList<string> Arguments) ParseApplicationCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new InvalidOperationException("Application command must not be empty.");

        if (ShellMetacharacters().IsMatch(command))
            throw new InvalidOperationException("Application command must be an executable plus arguments, not a shell expression.");

        var tokens = CommandToken().Matches(command)
            .Select(match => match.Groups["doubleQuoted"].Success
                ? match.Groups["doubleQuoted"].Value
                : match.Groups["singleQuoted"].Success
                    ? match.Groups["singleQuoted"].Value
                    : match.Groups["bare"].Value)
            .ToArray();
        if (tokens.Length == 0)
            throw new InvalidOperationException("Application command must not be empty.");
        if (tokens.Any(token => token.Contains('"') || token.Contains('\'')))
            throw new InvalidOperationException("Application command contains invalid quoting.");

        return ValidateParsedApplicationCommand(tokens);
    }

    private static (string FileName, IReadOnlyList<string> Arguments) ValidateParsedApplicationCommand(IReadOnlyList<string> tokens)
    {
        var fileName = ResolveAllowedApplicationExecutable(tokens[0]);

        if (tokens.Skip(1).Any(argument => argument.Any(char.IsControl)))
            throw new InvalidOperationException("Application command arguments must not contain control characters.");

        return (fileName, tokens.Skip(1).ToArray());
    }

    private static IReadOnlyList<string> ApplyWorkingDirectoryArguments(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var directory = CreateWorkspaceDirectoryAlias(workingDirectory);
        return fileName switch
        {
            "bun" => ["--cwd", directory, .. arguments],
            "dotnet" when arguments.Count > 0 && arguments[0] == "run" && !arguments.Contains("--project", StringComparer.Ordinal)
                => [.. arguments, "--project", directory],
            "dotnet" => QualifyOptionPathArgument(arguments, "--project", directory),
            "gradle" => ["-p", directory, .. arguments],
            "make" => ["-C", directory, .. arguments],
            "mvn" => ["-f", Path.Join(directory, "pom.xml"), .. arguments],
            "node" => QualifyFirstPathArgument(arguments, directory),
            "npm" => ["--prefix", directory, .. arguments],
            "pnpm" => ["--dir", directory, .. arguments],
            "yarn" => ["--cwd", directory, .. arguments],
            _ => arguments
        };
    }

    private static IReadOnlyList<string> QualifyFirstPathArgument(IReadOnlyList<string> arguments, string workingDirectory)
    {
        if (arguments.Count == 0 || arguments[0].StartsWith("-", StringComparison.Ordinal) || Path.IsPathRooted(arguments[0]))
            return arguments;

        return [Path.Join(workingDirectory, arguments[0]), .. arguments.Skip(1)];
    }

    private static IReadOnlyList<string> QualifyOptionPathArgument(
        IReadOnlyList<string> arguments,
        string option,
        string workingDirectory)
    {
        var qualified = arguments.ToArray();
        var optionIndex = Array.IndexOf(qualified, option);
        if (optionIndex < 0 || optionIndex == qualified.Length - 1 || Path.IsPathRooted(qualified[optionIndex + 1]))
            return qualified;

        qualified[optionIndex + 1] = Path.Join(workingDirectory, qualified[optionIndex + 1]);
        return qualified;
    }

    private static string CreateWorkspaceDirectoryAlias(string workingDirectory)
        => CreateWorkspaceDirectoryAlias(workingDirectory, WorkspaceDirectoryAliasRoot());

    private static string TrustedProcessWorkingDirectory()
        => AppContext.BaseDirectory;

    internal static string CreateWorkspaceDirectoryAlias(string workingDirectory, string aliasRoot)
    {
        Directory.CreateDirectory(aliasRoot);
        var aliasPath = WorkspaceDirectoryAliasPath(workingDirectory, aliasRoot);

        if (Directory.Exists(aliasPath))
            return VerifiedWorkspaceDirectoryAlias(aliasPath, workingDirectory);

        // A broken symlink (target deleted) is not a directory, so Directory.Exists returns
        // false above, but the symlink file still exists and blocks CreateSymbolicLink.
        var staleLink = new FileInfo(aliasPath);
        if (staleLink.LinkTarget is not null)
            staleLink.Delete();

        try
        {
            Directory.CreateSymbolicLink(aliasPath, workingDirectory);
        }
        catch (IOException) when (Directory.Exists(aliasPath) || new DirectoryInfo(aliasPath).LinkTarget is not null)
        {
            return VerifiedWorkspaceDirectoryAlias(aliasPath, workingDirectory);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return workingDirectory;
        }

        return VerifiedWorkspaceDirectoryAlias(aliasPath, workingDirectory);
    }

    internal static string WorkspaceDirectoryAliasPath(string workingDirectory, string aliasRoot)
    {
        var aliasName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workingDirectory)))[..32];
        return Path.Join(aliasRoot, aliasName);
    }

    private static string WorkspaceDirectoryAliasRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var aliasRootBase = string.IsNullOrWhiteSpace(localAppData)
            ? Path.GetTempPath()
            : localAppData;
        return Path.Join(aliasRootBase, "AgentUp", "WorkspaceDirectories");
    }

    private static string VerifiedWorkspaceDirectoryAlias(string aliasPath, string workingDirectory)
    {
        var target = Directory.ResolveLinkTarget(aliasPath, returnFinalTarget: true);
        if (target is not null && string.Equals(Path.GetFullPath(target.FullName), Path.GetFullPath(workingDirectory), StringComparison.Ordinal))
            return aliasPath;

        throw new InvalidOperationException("Workspace directory alias could not be verified.");
    }

    private static string ResolveAllowedApplicationExecutable(string fileName)
        => fileName switch
        {
            "bun" => "bun",
            "cargo" => "cargo",
            "dotnet" => "dotnet",
            "go" => "go",
            "gradle" => "gradle",
            "java" => "java",
            "make" => "make",
            "mvn" => "mvn",
            "node" => "node",
            "npm" => "npm",
            "npx" => "npx",
            "pnpm" => "pnpm",
            "printenv" => "printenv",
            "python" => "python",
            "python3" => "python3",
            "yarn" => "yarn",
            _ => throw new InvalidOperationException($"Application command executable '{fileName}' is not allowed.")
        };

    [GeneratedRegex(@"""(?<doubleQuoted>[^""]*)""|'(?<singleQuoted>[^']*)'|(?<bare>\S+)")]
    private static partial Regex CommandToken();

    [GeneratedRegex(@"[|&;<>()`$]|[\r\n]")]
    private static partial Regex ShellMetacharacters();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvironmentVariableName();
}
