using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Interfaces;

namespace AgentUp.Capabilities.Docker.Features.DockerCapability.Providers;

public sealed class DockerVersionProvider : IDockerVersionProvider
{
    private readonly CapabilityInventoryFileProvider _inventory;
    private readonly ICapabilityCommandRunner _commands;
    private readonly string _platform;

    public DockerVersionProvider()
        : this(new CapabilityInventoryFileProvider(), new ProcessCapabilityCommandRunner(), CurrentPlatform())
    {
    }

    public DockerVersionProvider(CapabilityInventoryFileProvider inventory)
        : this(inventory, new ProcessCapabilityCommandRunner(), CurrentPlatform())
    {
    }

    public DockerVersionProvider(
        CapabilityInventoryFileProvider inventory,
        ICapabilityCommandRunner commands,
        string platform)
    {
        _inventory = inventory;
        _commands = commands;
        _platform = platform;
    }

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var discovered = new List<CapabilityInstalledVersion>();
        discovered.AddRange(await _inventory.LoadAsync("docker", cancellationToken));
        discovered.AddRange(await DiscoverCliAsync(cancellationToken));
        discovered.AddRange(await DiscoverPackageManagersAsync(cancellationToken));
        return Deduplicate(discovered);
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverCliAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("docker", ["version", "--format", "{{.Client.Version}}"], cancellationToken);
        var version = result.Stdout.Trim();
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(version)
            ? [Installed(version, "docker")]
            : [];
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverPackageManagersAsync(CancellationToken cancellationToken)
        => _platform switch
        {
            "ubuntu" => await DiscoverAptAsync(cancellationToken),
            "macos" => await DiscoverHomebrewAsync(cancellationToken),
            "windows" => [.. await DiscoverChocolateyAsync(cancellationToken), .. await DiscoverWingetAsync(cancellationToken)],
            _ => []
        };

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAptAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("apt-cache", ["policy", "docker-ce"], cancellationToken);
        var version = ParseAptInstalledVersion(result.Stdout);
        return result.ExitCode == 0 && version is not null
            ? [Installed(version, "apt:docker-ce")]
            : [];
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverHomebrewAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("brew", ["list", "--versions", "docker"], cancellationToken);
        var version = ParsePackageVersionLine(result.Stdout, "docker");
        return result.ExitCode == 0 && version is not null
            ? [Installed(version, "brew:docker")]
            : [];
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverChocolateyAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("choco", ["list", "--local-only", "--exact", "docker-desktop"], cancellationToken);
        var version = ParsePackageVersionLine(result.Stdout, "docker-desktop");
        return result.ExitCode == 0 && version is not null
            ? [Installed(version, "choco:docker-desktop")]
            : [];
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverWingetAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("winget", ["list", "--id", "Docker.DockerDesktop", "--exact"], cancellationToken);
        var version = ParseWingetVersion(result.Stdout, "Docker.DockerDesktop");
        return result.ExitCode == 0 && version is not null
            ? [Installed(version, "winget:Docker.DockerDesktop")]
            : [];
    }

    private static CapabilityInstalledVersion Installed(string version, string location)
        => new("docker", version, location, CapabilityVersionSource.System, IsManaged: false);

    private static string? ParseAptInstalledVersion(string output)
        => output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("Installed:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line["Installed:".Length..].Trim())
            .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version) && version != "(none)");

    private static string? ParsePackageVersionLine(string output, string packageName)
        => output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(packageName + " ", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[(packageName.Length + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version));

    private static string? ParseWingetVersion(string output, string packageId)
        => output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Contains(packageId, StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault())
            .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version) && char.IsDigit(version[0]));

    private static IReadOnlyList<CapabilityInstalledVersion> Deduplicate(List<CapabilityInstalledVersion> versions)
        => versions
            .GroupBy(version => (version.CapabilityId, version.Version, version.Location), StringTupleComparer.Instance)
            .Select(group => group.First())
            .ToList();

    private static string CurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return "windows";
        if (OperatingSystem.IsMacOS())
            return "macos";
        if (OperatingSystem.IsLinux())
            return "ubuntu";
        return "unknown";
    }
}
