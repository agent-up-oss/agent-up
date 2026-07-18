using System.Diagnostics;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Interfaces;

namespace AgentUp.Capabilities.Docker.Features.DockerCapability.Providers;

public sealed class DockerVersionProvider : IDockerVersionProvider
{
    private readonly CapabilityInventoryFileProvider _inventory;

    public DockerVersionProvider()
        : this(new CapabilityInventoryFileProvider())
    {
    }

    public DockerVersionProvider(CapabilityInventoryFileProvider inventory)
    {
        _inventory = inventory;
    }

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var discovered = new List<CapabilityInstalledVersion>();
        discovered.AddRange(await _inventory.LoadAsync("docker", cancellationToken));

        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "version --format {{.Client.Version}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
                return discovered;

            var output = (await process.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return discovered;

            discovered.Add(new CapabilityInstalledVersion("docker", output, "docker", CapabilityVersionSource.System, IsManaged: false));
            return discovered;
        }
        catch
        {
            return discovered;
        }
    }
}
