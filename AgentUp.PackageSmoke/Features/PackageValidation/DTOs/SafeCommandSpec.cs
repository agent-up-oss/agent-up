namespace AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

internal readonly record struct SafeCommandSpec(
    SmokeExecutable Executable,
    string DisplayName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string>? Environment);
