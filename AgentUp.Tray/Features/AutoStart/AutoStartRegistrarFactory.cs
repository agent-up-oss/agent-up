namespace AgentUp.Tray.Features.AutoStart;

public static class AutoStartRegistrarFactory
{
    public static IAutoStartRegistrar? Create() => Create(CurrentPlatformId(), AppContext.BaseDirectory);

    /// <summary>
    /// Selects the registrar for a platform. Takes the platform and base directory rather
    /// than reading them statically, so every branch is reachable from any host instead of
    /// only the one the tests happen to run on.
    /// </summary>
    /// <param name="platformId">"windows", "macos" or "linux".</param>
    /// <param name="baseDirectory">Directory holding the tray executable.</param>
    public static IAutoStartRegistrar? Create(string platformId, string baseDirectory)
    {
        var executable = Path.Join(baseDirectory, TrayExecutableName(platformId));

        return platformId switch
        {
            "windows" => OperatingSystem.IsWindows() ? new WindowsAutoStartRegistrar(executable) : null,
            "macos" => new MacOsAutoStartRegistrar(executable),
            "linux" => new LinuxAutoStartRegistrar(executable),
            _ => null
        };
    }

    public static string TrayExecutableName(string platformId)
        => platformId == "windows" ? "AgentUp.Tray.exe" : "AgentUp.Tray";

    public static string CurrentPlatformId()
    {
        if (OperatingSystem.IsWindows())
            return "windows";

        if (OperatingSystem.IsMacOS())
            return "macos";

        return OperatingSystem.IsLinux() ? "linux" : "unsupported";
    }
}
