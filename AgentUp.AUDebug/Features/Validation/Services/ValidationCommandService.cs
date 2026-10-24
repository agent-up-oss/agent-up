using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Validation.DTOs;
using AgentUp.AUDebug.Features.Validation.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Validation.Services;

public sealed class ValidationCommandService(
    IDesktopValidationLaunch launch,
    IDebugEnvironment environment)
{
    public async Task<CommandResultDto> RunAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            return await RunCoreAsync(command, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s running validate.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            return CommandResultDto.Fail(ex.Message);
        }
    }

    private async Task<CommandResultDto> RunCoreAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        if (!string.Equals(command.Surface, DebugLayout.ValidateSurfaceDesktop, StringComparison.OrdinalIgnoreCase))
            return CommandResultDto.Fail("Error: --surface must be desktop.");

        var output = launch.ResolveOutputPath(command.OutDir);
        var result = await launch.RunAsync(
            new DesktopValidationLaunchSpec(
                output,
                command.FlowId,
                command.WorkspaceName,
                command.Password ?? environment.AdminPassword),
            cancellationToken);

        var message = CombinedMessage(result.StandardOutput, result.StandardError);
        if (result.ExitCode == 0)
            return CommandResultDto.Ok(
                string.IsNullOrWhiteSpace(message)
                    ? $"Desktop validation passed. Artifacts: {output}"
                    : message,
                output);

        return new CommandResultDto(
            1,
            string.IsNullOrWhiteSpace(message)
                ? $"Desktop validation failed. Artifacts: {output}"
                : message,
            output);
    }

    private static string CombinedMessage(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stdout))
            return stderr.Trim();
        if (string.IsNullOrWhiteSpace(stderr))
            return stdout.Trim();
        return $"{stdout.Trim()}{Environment.NewLine}{stderr.Trim()}";
    }
}
