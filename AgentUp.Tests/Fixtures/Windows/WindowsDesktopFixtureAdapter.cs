using System.Runtime.InteropServices;

namespace AgentUp.Tests.Fixtures.Windows;

public sealed class WindowsDesktopFixtureAdapter : IDesktopFixtureAdapter
{
    private DirectoryInfo? _fixtureProfile;
    private string? _originalLocalAppData;
    private string? _originalAppData;
    private string? _originalWebViewData;
    public string Name => "AgentUp.Fixtures.Windows";
    public bool RequiresStaThread => true;
    public bool RequiresSetupThreadAvalonia => false;
    public string StartupFailureHint => "Windows E2E tests require the hosted runner's desktop session and WebView2 runtime.";

    public void SetUp()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The Windows desktop fixture can only run on Windows.");

        var desktop = OpenInputDesktop(0, false, DesktopReadObjects | DesktopSwitchDesktop);
        if (desktop == IntPtr.Zero)
            throw new InvalidOperationException(StartupFailureHint);
        CloseDesktop(desktop);

        _fixtureProfile = Directory.CreateTempSubdirectory("agentup-e2e-windows-");
        _originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        _originalAppData = Environment.GetEnvironmentVariable("APPDATA");
        _originalWebViewData = Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Join(_fixtureProfile.FullName, "Local"));
        Environment.SetEnvironmentVariable("APPDATA", Path.Join(_fixtureProfile.FullName, "Roaming"));
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.Join(_fixtureProfile.FullName, "WebView2"));
        Environment.SetEnvironmentVariable("AGENTUP_E2E_PLATFORM", "windows");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _originalLocalAppData);
        Environment.SetEnvironmentVariable("APPDATA", _originalAppData);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", _originalWebViewData);
        _fixtureProfile?.Delete(recursive: true);
    }

    private const uint DesktopReadObjects = 0x0001;
    private const uint DesktopSwitchDesktop = 0x0100;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(IntPtr desktop);
}
