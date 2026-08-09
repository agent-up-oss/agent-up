namespace LocalInstaller.Core.Features.PrerequisiteChecks.Models;

public sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
