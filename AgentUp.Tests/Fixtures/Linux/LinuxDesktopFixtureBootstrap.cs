using System.Runtime.CompilerServices;

namespace AgentUp.Tests.Fixtures.Linux;

internal static class LinuxDesktopFixtureBootstrap
{
    [ModuleInitializer]
    internal static void IsolateBeforeNativeUiLoads()
    {
        if (!OperatingSystem.IsLinux())
            return;

        LinuxDesktopFixtureAdapter.EnsureIsolated();
    }
}
