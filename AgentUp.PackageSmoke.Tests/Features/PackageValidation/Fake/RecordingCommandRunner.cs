using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.PackageValidation.Fake;

internal sealed class RecordingCommandRunner : ICommandRunner
{
    private readonly Func<CommandSpec, CancellationToken, CommandResult> _handler;

    public RecordingCommandRunner(Func<CommandSpec, CancellationToken, CommandResult> handler)
    {
        _handler = handler;
    }

    public List<CommandSpec> Commands { get; } = [];

    public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
    {
        Commands.Add(command);
        return Task.FromResult(_handler(command, cancellationToken));
    }
}
