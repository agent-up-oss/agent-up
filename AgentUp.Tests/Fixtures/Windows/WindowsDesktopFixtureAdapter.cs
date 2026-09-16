namespace AgentUp.Tests.Fixtures.Windows;

public sealed class WindowsDesktopFixtureAdapter : IDesktopFixtureAdapter
{
    private DirectoryInfo? _fixtureProfile;
    private string? _originalWebViewData;
    public string Name => "AgentUp.Fixtures.Windows";
    public bool RequiresStaThread => true;
    public bool RequiresSetupThreadAvalonia => false;
    public string StartupFailureHint => "Windows E2E tests require the hosted runner's desktop session and WebView2 runtime.";

    // LOCALAPPDATA and APPDATA are deliberately left alone, unlike HOME on macOS.
    //
    // Redirecting them does not isolate anything Desktop stores: on Windows
    // Environment.GetFolderPath asks the shell for the profile path rather than reading the
    // environment, so FileServerConnectionStore and FileFirstRunTutorialSettingsStore keep
    // writing to the real profile either way. What does read the environment is the WebView2
    // browser process the platform WebView starts, and it cannot come up against an empty
    // profile root -- it never calls back, the native control host attachment never finishes,
    // and the whole E2E run hangs on the Avalonia UI thread with no test having started.
    // WEBVIEW2_USER_DATA_FOLDER is the supported way to move that profile, so use only that.
    public void SetUp()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The Windows desktop fixture can only run on Windows.");

        _fixtureProfile = Directory.CreateTempSubdirectory("agentup-e2e-windows-");
        _originalWebViewData = Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER");
        var webViewData = Directory.CreateDirectory(Path.Join(_fixtureProfile.FullName, "WebView2"));
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewData.FullName);
        Environment.SetEnvironmentVariable("AGENTUP_E2E_PLATFORM", "windows");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", _originalWebViewData);
        DeleteFixtureDirectory(_fixtureProfile);
    }

    private static void DeleteFixtureDirectory(DirectoryInfo? directory)
    {
        if (directory is null)
            return;

        try
        {
            directory.Delete(recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TestContext.Progress.WriteLine($"Could not remove Windows fixture directory '{directory.FullName}': {ex.Message}");
        }
    }
}
