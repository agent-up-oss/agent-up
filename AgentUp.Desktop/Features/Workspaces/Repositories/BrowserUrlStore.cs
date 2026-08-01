using System.Diagnostics;

namespace AgentUp.Desktop.Features.Workspaces.Repositories;

internal static class BrowserUrlStore
{
    // Overrideable in tests to avoid writing to the real profile directory.
    internal static string RootPath { get; set; } =
        Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "agentup", "profiles");

    internal static string ProfilePath(string workspaceId) =>
        Path.Join(RootPath, workspaceId);

    // Returns the saved URL for the port of baseUrl, only when the host:port matches.
    // One file per port so each tab within a workspace remembers its own last URL.
    internal static string? Read(string workspaceId, string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)) return null;
        try
        {
            var file = Path.Join(ProfilePath(workspaceId), $"last-url-{baseUri.Port}.txt");
            if (!File.Exists(file)) return null;
            var saved = File.ReadAllText(file).Trim();
            if (!Uri.TryCreate(saved, UriKind.Absolute, out var savedUri)) return null;
            return savedUri.Host == baseUri.Host && savedUri.Port == baseUri.Port ? saved : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    internal static void Write(string workspaceId, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        try
        {
            var dir = ProfilePath(workspaceId);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Join(dir, $"last-url-{uri.Port}.txt"), url);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }
}
