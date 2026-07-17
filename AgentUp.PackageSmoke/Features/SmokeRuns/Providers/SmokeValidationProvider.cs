using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Providers;

public sealed class SmokeValidationProvider : ISmokeValidationProvider
{
    private readonly ICommandRunner _commands;
    private readonly ISmokeWorkDirectoryProvider _workDirectory;

    public SmokeValidationProvider(ICommandRunner commands, ISmokeWorkDirectoryProvider workDirectory)
    {
        _commands = commands;
        _workDirectory = workDirectory;
    }

    public async Task<SmokeCommandResult> ValidatePackageAsync(
        SmokeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationRequest = new PackageValidationRequest(
            request.Platform,
            request.RuntimeId,
            request.ArtifactDirectory,
            request.WorkDirectory);
        var validator = PackageValidatorFactory.Create(validationRequest.Platform, _commands);
        var result = await validator.ValidateAsync(validationRequest, cancellationToken);
        await _workDirectory.WritePackageEnvironmentAsync(validationRequest.WorkDirectory, result, cancellationToken);
        return new SmokeCommandResult(result.Succeeded, result.Findings);
    }

    public async Task<SmokeCommandResult> ValidateInstallerFlowAsync(
        SmokeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PayloadRoot is not null)
            Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, request.PayloadRoot);

        var result = await new InstallerFlowSmokeValidator().ValidateAsync(request.Platform, request.WorkDirectory);
        return new SmokeCommandResult(result.Succeeded, result.Findings);
    }

    public async Task<SmokeCommandResult> ValidateInstalledServiceAsync(
        SmokeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        var smokeRequest = new InstalledServiceSmokeRequest(
            request.Platform,
            request.RuntimeId,
            request.ArtifactDirectory,
            request.WorkDirectory);
        var validator = InstalledServiceSmokeValidatorFactory.Create(smokeRequest.Platform, _commands, new HttpServerProbe());
        var result = await validator.ValidateAsync(smokeRequest, cancellationToken);
        return new SmokeCommandResult(result.Succeeded, result.Findings);
    }
}
