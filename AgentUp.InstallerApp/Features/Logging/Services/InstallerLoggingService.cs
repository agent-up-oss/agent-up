using AgentUp.InstallerApp.Features.Logging.Tools;

namespace AgentUp.InstallerApp.Features.Logging.Services;

public sealed class InstallerLoggingService
{
    public string FilePath => InstallerLog.FilePath;

    public void Write(string message)
        => InstallerLog.Write(message);

    public void WriteError(string message)
        => InstallerLog.WriteError(message);

    public void WriteException(string context, Exception exception)
        => InstallerLog.WriteException(context, exception);
}
