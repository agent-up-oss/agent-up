using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Server.Features.Capabilities.DTOs;

namespace AgentUp.Server.Features.Capabilities.Interfaces;

public interface ICapabilityEnabledSetStore
{
    EnabledCapabilitySetDto Read();
    void Write(EnabledCapabilitySetDto set);
}
