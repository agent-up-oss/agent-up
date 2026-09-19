namespace AgentUp.AUDebug.Features.Host.DTOs;

public static class DebugLayout
{
    public const int DefaultTimeoutSeconds = 30;
    public const int TestTimeoutSeconds = 180;
    public const int TestAllTimeoutSeconds = 600;
    public const int MaxTimeoutSeconds = 600;
    public const string ServerUrl = "http://127.0.0.1:5001";
    public const string DocsUrl = "http://127.0.0.1:10100";
    public const string DocsPath = "/design-system";
    public const string DocsHomePath = "/docs/";
    public const int DocsViewportWidth = 1440;
    public const int DocsViewportHeight = 900;
    public const int DocsMaxCaptureHeight = 16384;
    public const int DocsDebuggingPort = 19223;
    public const string MobileUrl = "http://127.0.0.1:10102";
    public const string DesktopWindowName = "Agent-Up";
    public const string DesktopWindowClass = "AgentUp.Desktop";
    public const int DesktopLoginFieldX = 550;
    public const int DesktopLoginFieldY = 420;
    public const int DesktopLoginButtonY = 478;
    public const int DesktopAgentTabX = 360;
    public const int DesktopAgentTabY = 52;
    public const int ReusedProcessPid = 0;
    public const string ServerReadyPath = "/api/auth/status";
}
