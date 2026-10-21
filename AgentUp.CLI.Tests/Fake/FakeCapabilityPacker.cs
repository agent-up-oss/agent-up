using AgentUp.CLI.Features.Capabilities.DTOs;
using AgentUp.CLI.Features.Capabilities.Interfaces;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class FakeCapabilityPacker : ICapabilityPacker
{
    public bool Succeeded { get; set; } = true;

    public string RegistryRoot { get; set; } = "/registry";

    public string Message { get; set; } = "Packed first-party capabilities into /registry";

    public int Calls { get; private set; }

    public CapabilityPackResult PackFirstParty(string workingDirectory)
    {
        Calls++;
        return new CapabilityPackResult(Succeeded, RegistryRoot, Message);
    }
}
