namespace AgentUp.Server.Features.Capabilities.Interfaces;

/// <summary>The values a user configured for each capability, secrets included.</summary>
public interface ICapabilitySettingsStore
{
    IReadOnlyDictionary<string, string> Read(string capabilityId);

    void Write(string capabilityId, IReadOnlyDictionary<string, string> values);
}
