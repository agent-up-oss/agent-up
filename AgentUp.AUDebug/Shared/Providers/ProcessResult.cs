namespace AgentUp.AUDebug.Shared.Providers;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
