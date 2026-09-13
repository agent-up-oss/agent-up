using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AgentUp.Tray.Features.AutoStart;

[SupportedOSPlatform("windows")]
public sealed class WindowsAutoStartRegistrar : IAutoStartRegistrar
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Agent-Up";

    private readonly string _exePath;

    public WindowsAutoStartRegistrar(string exePath)
    {
        _exePath = exePath;
    }

    public bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return WindowsRunKeyValue.Matches(key?.GetValue(ValueName) as string, _exePath);
    }

    public void Register()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException($"Cannot open registry key {RunKey}.");
        key.SetValue(ValueName, WindowsRunKeyValue.Format(_exePath));
    }

    public void Unregister()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
