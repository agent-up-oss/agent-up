namespace AgentUp.Desktop.Features.Workspaces.Views;

internal static class BrowserUrlStore
{
    // Overrideable in tests to avoid writing to the real profile directory.
    internal static string RootPath { get; set; } =
        Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "agentup", "profiles");

    internal static string ProfilePath(string workspaceId) =>
        Path.Join(RootPath, workspaceId);

    // Returns the saved URL only if it targets the same host:port as baseUrl; otherwise null.
    internal static string? Read(string workspaceId, string baseUrl)
    {
        try
        {
            var file = Path.Join(ProfilePath(workspaceId), "last-url.txt");
            if (!File.Exists(file)) return null;
            var saved = File.ReadAllText(file).Trim();
            if (!Uri.TryCreate(saved, UriKind.Absolute, out var savedUri) ||
                !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
                return null;
            return savedUri.Host == baseUri.Host && savedUri.Port == baseUri.Port ? saved : null;
        }
        catch { return null; }
    }

    internal static void Write(string workspaceId, string url)
    {
        try
        {
            var dir = ProfilePath(workspaceId);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Join(dir, "last-url.txt"), url);
        }
        catch { }
    }
}
