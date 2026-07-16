using AgentUp.PackageSmoke.Features.Validation;
using AgentUp.PackageSmoke.Features.Validation.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.Platforms;

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
