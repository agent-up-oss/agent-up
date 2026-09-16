using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Features.Host.Interfaces;

public interface IHostSessionStore
{
    HostSessionDto? Read();
    void Write(HostSessionDto session);
    void Delete();
    string ScreenshotPath(string surface);
    string ReadLogTail(string logPath, int lineCount);
}
