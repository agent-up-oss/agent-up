using System.Diagnostics;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Interfaces;

namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;

public sealed class DotnetVersionProvider : IDotnetVersionProvider
{
    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("dotnet", "--list-sdks")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
                return [];

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
                return [];

            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseSdk)
                .Where(version => version is not null)
                .Cast<CapabilityInstalledVersion>()
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static CapabilityInstalledVersion? ParseSdk(string line)
    {
        var bracket = line.IndexOf('[', StringComparison.Ordinal);
        var version = bracket < 0 ? line.Trim() : line[..bracket].Trim();
        if (string.IsNullOrWhiteSpace(version))
            return null;

        var location = bracket < 0 ? "dotnet" : line[(bracket + 1)..].TrimEnd(']', '\r', ' ');
        return new CapabilityInstalledVersion("dotnet", version, location, CapabilityVersionSource.System, IsManaged: false);
    }
}
