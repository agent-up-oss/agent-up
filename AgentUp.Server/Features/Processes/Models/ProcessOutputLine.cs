namespace AgentUp.Server.Features.Processes.Models;

public sealed record ProcessOutputLine(string Text, ProcessOutputStream Stream)
{
    public string StreamName => Stream == ProcessOutputStream.Stderr ? "stderr" : "stdout";
}
