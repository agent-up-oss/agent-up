using AgentUp.PackageSmoke.Features.Platforms;
using AgentUp.PackageSmoke.Features.Validation;
using AgentUp.PackageSmoke.Features.InstalledServices;

if (args.Length != 5 || args[0] is not ("validate-package" or "validate-installed-service"))
{
    Console.Error.WriteLine("Usage: AgentUp.PackageSmoke <validate-package|validate-installed-service> <platform> <runtime-id> <artifact-dir> <work-dir>");
    return 2;
}

var platform = args[1];
var runtimeId = args[2];
var artifactDirectory = Path.GetFullPath(args[3]);
var workDirectory = Path.GetFullPath(args[4]);

if (Directory.Exists(workDirectory))
    Directory.Delete(workDirectory, recursive: true);
Directory.CreateDirectory(workDirectory);

var commands = new ProcessCommandRunner();

if (args[0] == "validate-package")
{
    var request = new PackageValidationRequest(platform, runtimeId, artifactDirectory, workDirectory);
    var validator = PackageValidatorFactory.Create(request.Platform, commands);
    var result = await validator.ValidateAsync(request);
    await File.WriteAllTextAsync(Path.Combine(request.WorkDirectory, "package-smoke.env"), result.ToEnvironmentFile());
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
