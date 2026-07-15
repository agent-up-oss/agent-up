namespace AgentUp.Packaging.Features.ReleaseArtifacts;

public sealed record CommandSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null);

public sealed record CommandResult(int ExitCode, string Stdout, string Stderr);

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default);
}
