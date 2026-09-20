using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Tests.Support;

internal sealed class FixedCapabilityModuleLoader(CapabilityLoadedModule? module) : ICapabilityModuleLoader
{
    public CapabilityLoadedModule? Load(string packageDirectory, CapabilityPackageManifest manifest) => module;
}
