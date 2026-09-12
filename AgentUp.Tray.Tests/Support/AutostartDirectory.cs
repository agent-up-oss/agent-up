namespace AgentUp.Tray.Tests.Support;

/// <summary>
/// A throwaway directory standing in for the platform's autostart location, so the XDG and
/// launchd contracts are verified on any host rather than only on their own platform.
/// </summary>
internal sealed class AutostartDirectory : IDisposable
{
    private AutostartDirectory(string path) => Path = path;

    public string Path { get; }

    public static AutostartDirectory Create()
    {
        var path = System.IO.Path.Join(
            TestContext.CurrentContext.WorkDirectory, "autostart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new AutostartDirectory(path);
    }

    /// <summary>A directory that does not exist yet, so Register has to create it.</summary>
    public static AutostartDirectory Absent()
        => new(System.IO.Path.Join(
            TestContext.CurrentContext.WorkDirectory,
            "autostart-absent-" + Guid.NewGuid().ToString("N"),
            "nested"));

    public string FileAt(string name) => System.IO.Path.Join(Path, name);

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
