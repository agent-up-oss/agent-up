namespace AgentUp.Server.Features.ServiceControl.Interfaces;

public interface IProcessExitCode
{
    void Set(int code);
}

public sealed class ProcessExitCode : IProcessExitCode
{
    public void Set(int code) => Environment.ExitCode = code;
}
