using AgentUp.CLI.Features.Capabilities.DTOs;
using AgentUp.CLI.Features.Capabilities.Interfaces;
using AgentUp.CLI.Features.Capabilities.Models;

namespace AgentUp.CLI.Features.Capabilities.Providers;

public sealed class FirstPartyCapabilityPacker(
    ICapabilityPackProcessRunner processes,
    Func<string?>? registryPath = null) : ICapabilityPacker
{
    public async Task<CapabilityPackResult> PackFirstPartyAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var repositoryRoot = FindRepositoryRoot(workingDirectory);
        if (repositoryRoot is null)
        {
            return new CapabilityPackResult(
                false,
                "",
                "Packing first-party capabilities needs the Agent-Up repository "
                + $"(missing {FirstPartyCapabilities.PackScriptRelativePath}).");
        }

        var script = Path.Join(repositoryRoot, FirstPartyCapabilities.PackScriptRelativePath);
        var registryRoot = CapabilityRegistryRootResolver.Resolve(
            registryPath?.Invoke() ?? Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_REGISTRY_PATH"),
            repositoryRoot);

        CapabilityProcessResult ran;
        try
        {
            ran = await processes.RunAsync("bash", [script, registryRoot], repositoryRoot, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return new CapabilityPackResult(false, registryRoot, $"Error: Failed to pack capabilities: {ex.Message}");
        }

        if (ran.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(ran.StandardError) ? ran.StandardOutput : ran.StandardError;
            return new CapabilityPackResult(
                false,
                registryRoot,
                $"Error: Packing first-party capabilities failed.{Environment.NewLine}{detail}".Trim());
        }

        var packed = string.IsNullOrWhiteSpace(ran.StandardOutput)
            ? $"Packed first-party capabilities into {registryRoot}"
            : ran.StandardOutput.Trim();
        return new CapabilityPackResult(true, registryRoot, packed);
    }

    private static string? FindRepositoryRoot(string start)
    {
        var directory = Path.GetFullPath(start);
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Join(directory, FirstPartyCapabilities.PackScriptRelativePath)))
                return directory;

            var parent = Path.GetDirectoryName(directory);
            if (parent == directory)
                break;
            directory = parent ?? "";
        }

        return null;
    }
}
