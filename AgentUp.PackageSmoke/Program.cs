using AgentUp.PackageSmoke.Features.Platforms;
using AgentUp.PackageSmoke.Features.Validation;

if (args.Length != 5 || args[0] != "validate-package")
{
    Console.Error.WriteLine("Usage: AgentUp.PackageSmoke validate-package <platform> <runtime-id> <artifact-dir> <work-dir>");
    return 2;
}

var request = new PackageValidationRequest(args[1], args[2], Path.GetFullPath(args[3]), Path.GetFullPath(args[4]));
var validator = PackageValidatorFactory.Create(request.Platform, new ProcessCommandRunner());
var result = await validator.ValidateAsync(request);

Directory.CreateDirectory(request.WorkDirectory);
await File.WriteAllTextAsync(Path.Combine(request.WorkDirectory, "package-smoke.env"), result.ToEnvironmentFile());

foreach (var finding in result.Findings)
{
    var writer = finding.Severity == FindingSeverity.Error ? Console.Error : Console.Out;
    writer.WriteLine($"{finding.Severity}: {finding.Code}: {finding.Message}");
}

return result.Succeeded ? 0 : 1;
