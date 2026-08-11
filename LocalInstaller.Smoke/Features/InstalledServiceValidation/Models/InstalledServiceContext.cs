using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;

namespace LocalInstaller.Smoke.Features.InstalledServiceValidation.Models;

public sealed record InstalledServiceContext(
    string CliCommand,
    IReadOnlyDictionary<string, string>? CliEnvironment,
    IReadOnlyList<CommandSpec> UninstallCommands,
    IReadOnlyList<CommandSpec> DiagnosticCommands);
