namespace AgentUp.Desktop.Features.FirstRun.DTOs;

public sealed record FirstRunProcessResult(int ExitCode, string Stdout, string Stderr);
