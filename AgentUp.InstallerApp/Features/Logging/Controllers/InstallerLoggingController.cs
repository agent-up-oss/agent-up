using AgentUp.InstallerApp.Features.Logging.Services;

namespace AgentUp.InstallerApp.Features.Logging.Controllers;

public sealed class InstallerLoggingController
{
    private readonly InstallerLoggingService _service;

    public InstallerLoggingController(InstallerLoggingService service)
    {
        _service = service;
    }

    public string FilePath => _service.FilePath;

    public void Write(string message)
        => _service.Write(message);

    public void WriteError(string message)
        => _service.WriteError(message);

    public void WriteException(string context, Exception exception)
        => _service.WriteException(context, exception);
}
