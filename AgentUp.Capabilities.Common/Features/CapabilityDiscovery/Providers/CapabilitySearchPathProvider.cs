using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;

namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

public sealed class CapabilitySearchPathProvider : ICapabilitySearchPathProvider
{
    private readonly string _home;
    private readonly string _platform;
    private readonly IReadOnlyList<string> _extraDirectories;

    public CapabilitySearchPathProvider()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CurrentPlatform(), [])
    {
    }

    public CapabilitySearchPathProvider(string home, string platform, IReadOnlyList<string> extraDirectories)
    {
        _home = home;
        _platform = platform;
        _extraDirectories = extraDirectories;
    }

    public IReadOnlyList<string> Directories()
    {
        var directories = new List<string>();
        Add(directories, Path.Join(_home, ".local", "bin"));
        Add(directories, "/usr/local/bin");
        AddCursorAgentVersions(directories);
        if (_platform == "macos")
            Add(directories, "/opt/homebrew/bin");
        if (_platform == "windows")
        {
            Add(directories, Path.Join(_home, "AppData", "Roaming", "npm"));
            Add(directories, Path.Join(_home, "AppData", "Local", "npm"));
        }

        foreach (var extra in _extraDirectories)
            Add(directories, extra);

        return directories;
    }

    public static string CurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return "windows";
        if (OperatingSystem.IsMacOS())
            return "macos";
        if (OperatingSystem.IsLinux())
            return "ubuntu";
        return "unknown";
    }

    private void AddCursorAgentVersions(List<string> directories)
    {
        var versionsRoot = Path.Join(_home, ".local", "share", "cursor-agent", "versions");
        if (!TryGetDirectories(versionsRoot, out var versions))
            return;

        foreach (var version in versions)
        {
            var name = Path.GetFileName(version);
            if (name.StartsWith(".tmp-", StringComparison.Ordinal))
                continue;
            Add(directories, version);
        }
    }

    private static bool TryGetDirectories(string path, out string[] directories)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                directories = [];
                return false;
            }

            directories = Directory.GetDirectories(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            directories = [];
            return false;
        }
    }

    private static void Add(List<string> directories, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !directories.Contains(path, StringComparer.Ordinal))
            directories.Add(path);
    }
}
