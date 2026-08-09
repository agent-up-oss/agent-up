using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

public sealed record InstalledServiceContext(
    string CliCommand,
    IReadOnlyDictionary<string, string>? CliEnvironment,
    IReadOnlyList<CommandSpec> UninstallCommands,
    IReadOnlyList<CommandSpec> DiagnosticCommands);
