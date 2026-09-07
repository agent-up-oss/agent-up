using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Database.Models;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Database.Providers;

public sealed class DatabaseConnectionSettingsProvider
{
    public DatabaseConnectionSettings Resolve(Workspace workspace, ApplicationInstance app)
    {
        if (!app.Database)
            throw new InvalidOperationException($"Application '{app.Name}' is not configured as a database viewer target.");

        var engine = ResolveEngine(app);
        var environment = LoadEnvironment(workspace.WorktreePath, app);
        var port = ResolvePort(app);
        var username = GetEnvironmentValue(environment, "POSTGRES_USER", "POSTGRESQL_USER") ?? "postgres";
        var password = GetEnvironmentValue(environment, "POSTGRES_PASSWORD", "POSTGRESQL_PASSWORD") ?? string.Empty;
        var database = GetEnvironmentValue(environment, "POSTGRES_DB", "POSTGRESQL_DB") ?? username;

        return new DatabaseConnectionSettings(engine, "127.0.0.1", port, username, password, database);
    }

    private static string ResolveEngine(ApplicationInstance app)
    {
        if (app.Image?.Contains("postgres", StringComparison.OrdinalIgnoreCase) == true)
            return "postgres";

        throw new InvalidOperationException(
            $"Application '{app.Name}' has database viewer enabled but no supported database engine was detected.");
    }

    private static int ResolvePort(ApplicationInstance app)
    {
        var tcpPort = app.AllocatedPorts.FirstOrDefault(port =>
            string.Equals(port.Protocol, "tcp", StringComparison.OrdinalIgnoreCase));
        if (tcpPort is not null)
            return tcpPort.AllocatedPort;

        var anyPort = app.AllocatedPorts.FirstOrDefault();
        if (anyPort is not null)
            return anyPort.AllocatedPort;

        throw new InvalidOperationException($"Application '{app.Name}' has no allocated database port.");
    }

    private static Dictionary<string, string> LoadEnvironment(string worktreePath, ApplicationInstance app)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var environmentFile in app.EnvironmentFiles ?? [])
        {
            foreach (var line in DatabaseWorkspaceFileProvider.ReadEnvironmentFileLines(worktreePath, environmentFile))
                TryParseEnvironmentLine(line, environment);
        }

        foreach (var (key, value) in app.Environment ?? new Dictionary<string, string>())
            environment[key] = value;

        return environment;
    }

    private static void TryParseEnvironmentLine(string rawLine, IDictionary<string, string> environment)
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
            return;

        if (line.StartsWith("export ", StringComparison.Ordinal))
            line = line["export ".Length..].TrimStart();

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
            return;

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();
        if (value.Length >= 2
            && ((value.StartsWith('"') && value.EndsWith('"'))
                || (value.StartsWith('\'') && value.EndsWith('\''))))
            value = value[1..^1];

        environment[key] = value;
    }

    private static string? GetEnvironmentValue(
        IReadOnlyDictionary<string, string> environment,
        params string[] keys)
        => keys.Select(key => environment.TryGetValue(key, out var value) ? value : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
