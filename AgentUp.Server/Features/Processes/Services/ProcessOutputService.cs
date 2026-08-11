using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Models;
using AgentUp.Server.Shared.Providers;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Processes.Services;

public sealed class ProcessOutputService
{
    private readonly IOutputRepository _output;
    private readonly AuditController? _audit;
    private readonly ILogger<ProcessOutputService>? _logger;
    private readonly ConsoleSecretRedactor _redactor;

    public ProcessOutputService(IOutputRepository output)
    {
        _output = output;
        _redactor = new ConsoleSecretRedactor();
    }

    public ProcessOutputService(
        IOutputRepository output,
        AuditController audit,
        ILogger<ProcessOutputService> logger)
        : this(output, audit, logger, new ConsoleSecretRedactor())
    {
    }

    public ProcessOutputService(
        IOutputRepository output,
        AuditController? audit,
        ILogger<ProcessOutputService>? logger,
        ConsoleSecretRedactor redactor)
    {
        _output = output;
        _audit = audit;
        _logger = logger;
        _redactor = redactor;
    }

    public async Task AppendAsync(
        string workspaceId,
        string applicationName,
        string line,
        CancellationToken cancellationToken = default)
        => await AppendAsync(workspaceId, applicationName, line, ProcessOutputStream.Stdout, cancellationToken);

    public async Task AppendAsync(
        string workspaceId,
        string applicationName,
        string line,
        ProcessOutputStream stream,
        CancellationToken cancellationToken = default)
    {
        await _output.AppendAsync(workspaceId, applicationName, line, cancellationToken);
        await RecordAuditEventAsync(workspaceId, applicationName, line, stream);
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
        string line,
        ProcessOutputStream stream)
    {
        if (_audit is null)
            return;

        try
        {
            var streamName = stream == ProcessOutputStream.Stderr ? "stderr" : "stdout";
            var message = stream == ProcessOutputStream.Stderr && line.StartsWith("[err] ", StringComparison.Ordinal)
                ? line[6..]
                : line;

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
                        ["stream"] = streamName,
                        ["message"] = _redactor.Redact(message)
                    }),
                CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Failed to record application console audit event");
        }
    }
}
