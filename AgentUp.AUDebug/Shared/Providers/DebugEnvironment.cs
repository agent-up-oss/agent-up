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

    public string? FindChromium() =>
        ChromiumNames
            .Select(FindOnPath)
            .FirstOrDefault(path => path is not null && !IsInjectingWrapper(path));

    private static readonly string[] ChromiumNames =
    [
        "chromium",
        "chromium-browser",
        "google-chrome-stable",
        "google-chrome"
    ];

    private static bool IsInjectingWrapper(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[2];
            if (stream.Read(header) != 2 || header[0] != (byte)'#' || header[1] != (byte)'!')
                return false;
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Contains("--remote-debugging-port", StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}
