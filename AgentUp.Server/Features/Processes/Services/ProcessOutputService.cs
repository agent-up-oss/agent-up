using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Processes.Services;

public sealed class ProcessOutputService
{
    private readonly IOutputRepository _output;
    private readonly AuditController? _audit;
    private readonly ILogger<ProcessOutputService>? _logger;

    public ProcessOutputService(IOutputRepository output)
    {
        _output = output;
    }

    public ProcessOutputService(
        IOutputRepository output,
        AuditController audit,
        ILogger<ProcessOutputService> logger)
    {
        _output = output;
        _audit = audit;
        _logger = logger;
    }

    public async Task AppendAsync(
        string workspaceId,
        string applicationName,
        string line,
        CancellationToken cancellationToken = default)
    {
        await _output.AppendAsync(workspaceId, applicationName, line, cancellationToken);
        await RecordAuditEventAsync(workspaceId, applicationName, line);
    }

    public async Task<IReadOnlyList<string>> GetAsync(string workspaceId, string applicationName)
        => await _output.GetAsync(workspaceId, applicationName);

    public async Task ClearAsync(
        string workspaceId,
        string applicationName,
        CancellationToken cancellationToken = default)
        => await _output.ClearAsync(workspaceId, applicationName, cancellationToken);

    private async Task RecordAuditEventAsync(
        string workspaceId,
        string applicationName,
        string line)
    {
        if (_audit is null)
            return;

        try
        {
            var stream = line.StartsWith("[err] ", StringComparison.Ordinal)
                ? "stderr"
                : "stdout";
            var message = stream == "stderr" ? line[6..] : line;

            await _audit.RecordAsync(
                new AuditRecordRequest(
                    "application",
                    "process",
                    "application_console_line",
                    "success",
                    workspaceId,
                    new Dictionary<string, string>
                    {
                        ["applicationName"] = applicationName,
                        ["stream"] = stream,
                        ["message"] = message
                    }),
                CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Failed to record application console audit event");
        }
    }
}
