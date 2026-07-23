namespace AgentUp.PackageSmoke.Features.PackageValidation.Models;

internal readonly record struct SafeCommandSpec(
    SmokeExecutable Executable,
    string DisplayName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string>? Environment);
