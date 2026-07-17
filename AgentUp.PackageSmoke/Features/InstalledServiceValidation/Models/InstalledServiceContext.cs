using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

public sealed record InstalledServiceContext(
    string CliCommand,
    IReadOnlyDictionary<string, string>? CliEnvironment,
    IReadOnlyList<CommandSpec> UninstallCommands,
    IReadOnlyList<CommandSpec> DiagnosticCommands);
