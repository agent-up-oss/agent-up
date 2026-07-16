using AgentUp.PackageSmoke.Features.Platforms;
using AgentUp.PackageSmoke.Features.Validation;
using AgentUp.PackageSmoke.Features.InstalledServices;
using AgentUp.PackageSmoke.Features.InstallerFlow;
using AgentUp.Installers.Features.Execution;

if ((args.Length != 5 || args[0] is not ("validate-package" or "validate-installed-service"))
    && (args.Length is not (3 or 4) || args[0] != "validate-installer-flow"))
{
    Console.Error.WriteLine("Usage: AgentUp.PackageSmoke <validate-package|validate-installed-service> <platform> <runtime-id> <artifact-dir> <work-dir>");
    Console.Error.WriteLine("   or: AgentUp.PackageSmoke validate-installer-flow <platform> <work-dir> [payload-root]");
    return 2;
}

var platform = args[1];
var runtimeId = args[0] == "validate-installer-flow" ? "" : args[2];
var artifactDirectory = args[0] == "validate-installer-flow" ? "" : Path.GetFullPath(args[3]);
var workDirectory = Path.GetFullPath(args[0] == "validate-installer-flow" ? args[2] : args[4]);

if (Directory.Exists(workDirectory))
    Directory.Delete(workDirectory, recursive: true);
Directory.CreateDirectory(workDirectory);

var commands = new ProcessCommandRunner();

if (args[0] == "validate-package")
{
    var request = new PackageValidationRequest(platform, runtimeId, artifactDirectory, workDirectory);
    var validator = PackageValidatorFactory.Create(request.Platform, commands);
    var result = await validator.ValidateAsync(request);
    await File.WriteAllTextAsync(Path.Join(request.WorkDirectory, "package-smoke.env"), result.ToEnvironmentFile());
    WriteFindings(result.Findings);
    return result.Succeeded ? 0 : 1;
}

if (args[0] == "validate-installer-flow")
{
    if (args.Length == 4)
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, Path.GetFullPath(args[3]));

    var result = await new InstallerFlowSmokeValidator().ValidateAsync(platform, workDirectory);
    WriteFindings(result.Findings);
    return result.Succeeded ? 0 : 1;
}

var installedRequest = new InstalledServiceSmokeRequest(platform, runtimeId, artifactDirectory, workDirectory);
var installedValidator = InstalledServiceSmokeValidatorFactory.Create(installedRequest.Platform, commands, new HttpServerProbe());
var installedResult = await installedValidator.ValidateAsync(installedRequest);
WriteFindings(installedResult.Findings);
return installedResult.Succeeded ? 0 : 1;

static void WriteFindings(IReadOnlyList<SmokeFinding> findings)
{
    foreach (var finding in findings)
    {
        var writer = finding.Severity == FindingSeverity.Error ? Console.Error : Console.Out;
        writer.WriteLine($"{finding.Severity}: {finding.Code}: {finding.Message}");
    }
}
