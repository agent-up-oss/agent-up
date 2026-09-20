using System.Reflection;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Capabilities.Providers;

public sealed class CapabilityModuleLoadProvider : ICapabilityModuleLoader
{
    public CapabilityLoadedModule? Load(string packageDirectory, CapabilityPackageManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Module))
            return null;

        var path = Path.GetFullPath(Path.Join(packageDirectory, manifest.Module));
        var root = Path.GetFullPath(packageDirectory);
        if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
            return null;

        var assembly = Assembly.LoadFrom(path);
        var types = SafeTypes(assembly).OfType<Type>().Where(type => !type.IsAbstract).ToArray();
        var runtime = types
            .Where(type => typeof(RuntimeCapabilityProjectDefinition).IsAssignableFrom(type))
            .Select(type => CreateRuntime((RuntimeCapabilityProjectDefinition)Activator.CreateInstance(type)!))
            .FirstOrDefault(loaded => loaded is not null);
        var agent = types
            .Where(type => typeof(AgentCapabilityProjectDefinition).IsAssignableFrom(type))
            .Select(type => CreateAgent((AgentCapabilityProjectDefinition)Activator.CreateInstance(type)!))
            .FirstOrDefault(loaded => loaded is not null);

        if (runtime is null && agent is null)
            return null;
        return new CapabilityLoadedModule(runtime, agent);
    }

    private static IRuntimeCapability? CreateRuntime(RuntimeCapabilityProjectDefinition definition)
    {
        var registration = definition.CreateProject().Registrations.FirstOrDefault();
        return registration is null
            ? null
            : Activator.CreateInstance(registration.ImplementationType) as IRuntimeCapability;
    }

    private static IAgentCapability? CreateAgent(AgentCapabilityProjectDefinition definition)
    {
        var registration = definition.CreateProject().Registrations.FirstOrDefault();
        return registration is null
            ? null
            : Activator.CreateInstance(registration.ImplementationType) as IAgentCapability;
    }

    private static IEnumerable<Type?> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types;
        }
    }
}
