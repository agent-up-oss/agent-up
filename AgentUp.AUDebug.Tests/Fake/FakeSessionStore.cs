using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeSessionStore : IHostSessionStore
{
    public HostSessionDto? Session { get; set; }
    public string NextScreenshot { get; set; } = "/tmp/shot.png";
    public string LogTail { get; set; } = "log-tail";
    public int Writes { get; private set; }
    public int Deletes { get; private set; }

    public HostSessionDto? Read() => Session;
    public void Write(HostSessionDto session)
    {
        Writes++;
        Session = session;
    }

    public void Delete()
    {
        Deletes++;
        Session = null;
    }

    public string ScreenshotPath(string surface) => NextScreenshot.Replace("shot", surface, StringComparison.Ordinal);
    public string ReadLogTail(string logPath, int lineCount) => LogTail;
}
