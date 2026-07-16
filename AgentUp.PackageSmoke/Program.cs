using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;

var request = SmokeCommandRequest.TryParse(args);
if (request is null)
{
    Console.Error.WriteLine("Usage: AgentUp.PackageSmoke <validate-package|validate-installed-service> <platform> <runtime-id> <artifact-dir> <work-dir>");
    Console.Error.WriteLine("   or: AgentUp.PackageSmoke validate-installer-flow <platform> <work-dir> [payload-root]");
    return 2;
}

PrepareWorkDirectory(request.WorkDirectory);

var result = await RunAsync(request);
WriteFindings(result.Findings);
return result.Succeeded ? 0 : 1;

static async Task<SmokeCommandResult> RunAsync(SmokeCommandRequest request)
{
    var commands = new ProcessCommandRunner();

    return request.Command switch
    {
        "validate-package" => await ValidatePackageAsync(request, commands),
        "validate-installer-flow" => await ValidateInstallerFlowAsync(request),
        _ => await ValidateInstalledServiceAsync(request, commands)
    };
}

static async Task<SmokeCommandResult> ValidatePackageAsync(
    SmokeCommandRequest request,
    ProcessCommandRunner commands)
{
    var validationRequest = new PackageValidationRequest(
        request.Platform,
        request.RuntimeId,
        request.ArtifactDirectory,
        request.WorkDirectory);
    var validator = PackageValidatorFactory.Create(validationRequest.Platform, commands);
    var result = await validator.ValidateAsync(validationRequest);
    await File.WriteAllTextAsync(Path.Join(validationRequest.WorkDirectory, "package-smoke.env"), result.ToEnvironmentFile());
    return new SmokeCommandResult(result.Succeeded, result.Findings);
}

static async Task<SmokeCommandResult> ValidateInstallerFlowAsync(SmokeCommandRequest request)
{
    if (request.PayloadRoot is not null)
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, request.PayloadRoot);

    var result = await new InstallerFlowSmokeValidator().ValidateAsync(request.Platform, request.WorkDirectory);
    return new SmokeCommandResult(result.Succeeded, result.Findings);
}

static async Task<SmokeCommandResult> ValidateInstalledServiceAsync(
    SmokeCommandRequest request,
    ProcessCommandRunner commands)
{
    var smokeRequest = new InstalledServiceSmokeRequest(
        request.Platform,
        request.RuntimeId,
        request.ArtifactDirectory,
        request.WorkDirectory);
    var validator = InstalledServiceSmokeValidatorFactory.Create(smokeRequest.Platform, commands, new HttpServerProbe());
    var result = await validator.ValidateAsync(smokeRequest);
    return new SmokeCommandResult(result.Succeeded, result.Findings);
}

static void PrepareWorkDirectory(string workDirectory)
{
    if (Directory.Exists(workDirectory))
        Directory.Delete(workDirectory, recursive: true);
    Directory.CreateDirectory(workDirectory);
}

static void WriteFindings(IReadOnlyList<SmokeFinding> findings)
{
    foreach (var finding in findings)
    {
        var writer = finding.Severity == FindingSeverity.Error ? Console.Error : Console.Out;
        writer.WriteLine($"{finding.Severity}: {finding.Code}: {finding.Message}");
    }
}

internal sealed record SmokeCommandRequest(
    string Command,
    string Platform,
    string RuntimeId,
    string ArtifactDirectory,
    string WorkDirectory,
    string? PayloadRoot)
{
    public static SmokeCommandRequest? TryParse(string[] args)
    {
        if (args.Length == 3 && args[0] == "validate-installer-flow")
            return InstallerFlow(args[1], args[2], payloadRoot: null);

        if (args.Length == 4 && args[0] == "validate-installer-flow")
            return InstallerFlow(args[1], args[2], Path.GetFullPath(args[3]));

        if (args.Length == 5 && args[0] is "validate-package" or "validate-installed-service")
        {
            return new SmokeCommandRequest(
                args[0],
                args[1],
                args[2],
                Path.GetFullPath(args[3]),
                Path.GetFullPath(args[4]),
                PayloadRoot: null);
        }

        return null;
    }

    private static SmokeCommandRequest InstallerFlow(string platform, string workDirectory, string? payloadRoot)
        => new(
            "validate-installer-flow",
            platform,
            RuntimeId: "",
            ArtifactDirectory: "",
            Path.GetFullPath(workDirectory),
            payloadRoot);
}

internal sealed record SmokeCommandResult(bool Succeeded, IReadOnlyList<SmokeFinding> Findings);
