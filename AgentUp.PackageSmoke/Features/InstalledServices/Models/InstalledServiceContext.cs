using AgentUp.PackageSmoke.Features.Validation.Providers;

namespace AgentUp.PackageSmoke.Features.InstalledServices.Models;

public sealed record InstalledServiceContext(
    string CliPath,
    IReadOnlyList<CommandSpec> UninstallCommands,
    IReadOnlyList<CommandSpec> DiagnosticCommands);
