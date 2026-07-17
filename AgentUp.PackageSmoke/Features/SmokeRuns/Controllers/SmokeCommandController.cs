using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;
using AgentUp.PackageSmoke.Features.SmokeRuns.Services;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Controllers;

public sealed class SmokeCommandController
{
    private readonly ISmokeCommandParser _parser;
    private readonly ISmokeWorkDirectoryProvider _workDirectory;
    private readonly SmokeCommandService _service;

    public SmokeCommandController(
        ISmokeCommandParser parser,
        ISmokeWorkDirectoryProvider workDirectory,
        SmokeCommandService service)
    {
        _parser = parser;
        _workDirectory = workDirectory;
        _service = service;
    }

    public async Task<int> ExecuteAsync(
        string[] args,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        var parsed = _parser.Parse(args);
        if (!parsed.Succeeded)
        {
            standardError.WriteLine(parsed.Usage);
            return 2;
        }

        var request = parsed.Request!;
        _workDirectory.Prepare(request.WorkDirectory);
        var result = await _service.RunAsync(request, cancellationToken);
        WriteFindings(result.Findings, standardOutput, standardError);
        return result.Succeeded ? 0 : 1;
    }

    private static void WriteFindings(
        IReadOnlyList<SmokeFinding> findings,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        foreach (var finding in findings)
        {
            var writer = finding.Severity == FindingSeverity.Error ? standardError : standardOutput;
            writer.WriteLine($"{finding.Severity}: {finding.Code}: {finding.Message}");
        }
    }
}
