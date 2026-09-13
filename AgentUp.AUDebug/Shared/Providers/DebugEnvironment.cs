using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Shared.Providers;

public sealed class DebugEnvironment : IDebugEnvironment
{
    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string Display => GetVariable("DISPLAY") ?? ":0";

    public string? AdminPassword => GetVariable("AGENTUP_ADMIN_PASSWORD");

    public string? FindOnPath(string executableName)
    {
        var path = GetVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var directories = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return directories
            .Select(directory => Path.Join(directory, executableName))
            .FirstOrDefault(File.Exists);
    }
}
