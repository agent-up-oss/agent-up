namespace AgentUp.Server.Features.ServiceControl.Interfaces;

public interface IProcessExitCode
{
    void Set(int code);

    // Terminates the process immediately without notifying the service manager,
    // so Windows SCM / systemd / launchd all see an unexpected exit and restart the service.
    void Exit(int code);
}

public sealed class ProcessExitCode : IProcessExitCode
{
    public void Set(int code) => Environment.ExitCode = code;
    public void Exit(int code) => Environment.Exit(code);
}
