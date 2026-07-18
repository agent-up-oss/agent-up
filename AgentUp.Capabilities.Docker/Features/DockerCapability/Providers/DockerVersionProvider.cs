using System.Diagnostics;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Interfaces;

namespace AgentUp.Capabilities.Docker.Features.DockerCapability.Providers;

public sealed class DockerVersionProvider : IDockerVersionProvider
{
    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
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
                return [];

            var output = (await process.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return [];

            return
            [
                new CapabilityInstalledVersion("docker", output, "docker", CapabilityVersionSource.System, IsManaged: false)
            ];
        }
        catch
        {
            return [];
        }
    }
}
