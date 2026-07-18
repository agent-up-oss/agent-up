using System.Diagnostics;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Interfaces;

namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;

public sealed class DotnetVersionProvider : IDotnetVersionProvider
{
    private readonly CapabilityInventoryFileProvider _inventory;

    public DotnetVersionProvider()
        : this(new CapabilityInventoryFileProvider())
    {
    }

    public DotnetVersionProvider(CapabilityInventoryFileProvider inventory)
    {
        _inventory = inventory;
    }

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var discovered = new List<CapabilityInstalledVersion>();
        discovered.AddRange(await _inventory.LoadAsync("dotnet", cancellationToken));

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
                return discovered;

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
                return discovered;

            discovered.AddRange(output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseSdk)
                .Where(version => version is not null)
                .Cast<CapabilityInstalledVersion>()
                .ToList());
            return discovered;
        }
        catch
        {
            return discovered;
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
