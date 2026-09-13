using System.ComponentModel;
using System.Diagnostics;

namespace AgentUp.Tray.Features.AutoStart;

/// <summary>
/// Runs launchctl. This is the whole of the macOS registrar's contact with the operating
/// system, kept in its own file so the registrar's plist contract and load/unload sequence
/// can be injected and verified while the one call that cannot be made in a test stays
/// isolated and identifiable.
/// </summary>
public static class LaunchctlProcess
{
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(3);

    public static void Run(string verb, string plistPath)
    {
        try
        {
            using var launchctl = Process.Start("launchctl", [verb, plistPath]);
            launchctl?.WaitForExit(ExitWait);
        }
        catch (Exception ex) when (ex is Win32Exception
                                       or InvalidOperationException
                                       or PlatformNotSupportedException)
        {
            // launchctl unavailable, or the job is already in the requested state.
        }
    }
}
