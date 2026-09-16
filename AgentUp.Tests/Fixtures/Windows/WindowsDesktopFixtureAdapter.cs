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

        _fixtureProfile = Directory.CreateTempSubdirectory("agentup-e2e-windows-");
        _originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        _originalAppData = Environment.GetEnvironmentVariable("APPDATA");
        _originalWebViewData = Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER");
        var localAppData = Directory.CreateDirectory(Path.Join(_fixtureProfile.FullName, "Local"));
        var appData = Directory.CreateDirectory(Path.Join(_fixtureProfile.FullName, "Roaming"));
        var webViewData = Directory.CreateDirectory(Path.Join(_fixtureProfile.FullName, "WebView2"));
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData.FullName);
        Environment.SetEnvironmentVariable("APPDATA", appData.FullName);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewData.FullName);
        Environment.SetEnvironmentVariable("AGENTUP_E2E_PLATFORM", "windows");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _originalLocalAppData);
        Environment.SetEnvironmentVariable("APPDATA", _originalAppData);
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
