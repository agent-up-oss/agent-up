using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Validate.DTOs;
using AgentUp.AUDebug.Features.Validate.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Validate.Services;

public sealed class ValidateCommandService
{
    private readonly IMobileValidationRunner _mobile;
    private readonly IDebugPathValidator _paths;

    public ValidateCommandService(IMobileValidationRunner mobile, IDebugPathValidator paths)
    {
        _mobile = mobile;
        _paths = paths;
    }

    public async Task<CommandResultDto> RunAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        if (string.Equals(command.Surface, "desktop", StringComparison.OrdinalIgnoreCase))
            return CommandResultDto.Fail("Desktop surface replay is not implemented.");
        if (!string.Equals(command.Surface, "mobile", StringComparison.OrdinalIgnoreCase))
            return CommandResultDto.Fail($"Error: unknown validation surface '{command.Surface}'. Choose mobile.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            var outDir = string.IsNullOrWhiteSpace(command.OutDir)
                ? _paths.JoinUnderRoot(DebugLayout.MobileValidationDirectory)
                : _paths.EnsureUnderRoot(Path.GetFullPath(command.OutDir, _paths.RepositoryRoot));
            var result = await _mobile.RunAsync(new MobileValidationRunDto(command.FlowId, outDir), timeout.Token);
            var transcript = Combine(result.StandardOutput, result.StandardError);
            var report = Path.Join(outDir, "report.json");
            if (result.ExitCode != 0)
                return CommandResultDto.Fail(string.IsNullOrWhiteSpace(transcript)
                    ? $"Mobile validation failed ({result.ExitCode})."
                    : transcript.TrimEnd());
            return CommandResultDto.Ok(
                string.IsNullOrWhiteSpace(transcript) ? "Mobile validation passed." : transcript.TrimEnd(),
                report);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s running au-debug validate --surface mobile.");
        }
    }

    private static string Combine(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
            return stdout;
        if (string.IsNullOrWhiteSpace(stdout))
            return stderr;
        return stdout.TrimEnd() + Environment.NewLine + stderr;
    }
}
