using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Services;

public sealed class SmokeCommandService
{
    private readonly ISmokeValidationProvider _validation;
    private readonly ISmokeWorkDirectoryProvider _workDirectory;
    private readonly ISmokeCommandParser _parser;

    public SmokeCommandService(
        ISmokeValidationProvider validation,
        ISmokeWorkDirectoryProvider workDirectory,
        ISmokeCommandParser parser)
    {
        _validation = validation;
        _workDirectory = workDirectory;
        _parser = parser;
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

        var result = await ExecuteAsync(parsed.Request!, cancellationToken);
        WriteFindings(result.Findings, standardOutput, standardError);
        return result.Succeeded ? 0 : 1;
    }

    public Task<SmokeCommandResult> ExecuteAsync(SmokeCommandRequest request, CancellationToken cancellationToken = default)
    {
        _workDirectory.Prepare(request.WorkDirectory);
        return RunAsync(request, cancellationToken);
    }

    public Task<SmokeCommandResult> RunAsync(SmokeCommandRequest request, CancellationToken cancellationToken = default)
        => request.Command switch
        {
            "validate-package" => _validation.ValidatePackageAsync(request, cancellationToken),
            "validate-installer-flow" => _validation.ValidateInstallerFlowAsync(request, cancellationToken),
            _ => _validation.ValidateInstalledServiceAsync(request, cancellationToken)
        };

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
