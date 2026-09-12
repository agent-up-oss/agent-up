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
    public const string MobileUrl = "http://127.0.0.1:10102";
    public const string DesktopWindowName = "Agent-Up";
    public const string DesktopWindowClass = "AgentUp.Desktop";
    public const string ServerReadyPath = "/api/auth/status";
}
