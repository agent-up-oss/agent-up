using System.Runtime.InteropServices;

namespace AgentUp.Tray.Features.AutoStart;

public static class AutoStartRegistrarFactory
{
    public static IAutoStartRegistrar? Create()
    {
        var exe = Path.Join(AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "AgentUp.Tray.exe" : "AgentUp.Tray");

        if (OperatingSystem.IsWindows())
            return new WindowsAutoStartRegistrar(exe);

        if (OperatingSystem.IsMacOS())
            return new MacOsAutoStartRegistrar(exe);

        if (OperatingSystem.IsLinux())
            return new LinuxAutoStartRegistrar(exe);

        return null;
    }
}
