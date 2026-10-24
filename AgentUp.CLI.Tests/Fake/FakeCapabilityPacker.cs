using AgentUp.CLI.Features.Capabilities.DTOs;
using AgentUp.CLI.Features.Capabilities.Interfaces;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class FakeCapabilityPacker : ICapabilityPacker
{
    public bool Succeeded { get; set; } = true;

    public string RegistryRoot { get; set; } = "/registry";

    public string Message { get; set; } = "Packed first-party capabilities into /registry";

    public int Calls { get; private set; }

    public Task<CapabilityPackResult> PackFirstPartyAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(new CapabilityPackResult(Succeeded, RegistryRoot, Message));
    }
}
