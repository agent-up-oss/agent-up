using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Interfaces;

namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;

public sealed class DotnetVersionProvider : IDotnetVersionProvider
{
    private readonly CapabilityInventoryFileProvider _inventory;
    private readonly ICapabilityCommandRunner _commands;
    private readonly string _platform;

    public DotnetVersionProvider()
        : this(new CapabilityInventoryFileProvider(), new ProcessCapabilityCommandRunner(), CurrentPlatform())
    {
    }

    public DotnetVersionProvider(CapabilityInventoryFileProvider inventory)
        : this(inventory, new ProcessCapabilityCommandRunner(), CurrentPlatform())
    {
    }

    public DotnetVersionProvider(
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
        discovered.AddRange(await _inventory.LoadAsync("dotnet", cancellationToken));
        discovered.AddRange(await DiscoverCliAsync(cancellationToken));
        discovered.AddRange(await DiscoverPackageManagersAsync(cancellationToken));
        return Deduplicate(discovered);
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverCliAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("dotnet", ["--list-sdks"], cancellationToken);
        if (result.ExitCode != 0)
            return [];

        return result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseSdk)
                .Where(version => version is not null)
                .Cast<CapabilityInstalledVersion>()
                .ToList();
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
        var result = await _commands.RunAsync("apt-cache", ["policy", "dotnet-sdk-10.0"], cancellationToken);
        var version = ParseAptInstalledVersion(result.Stdout);
        return result.ExitCode == 0 && version is not null
            ? [Installed(version, "apt:dotnet-sdk-10.0")]
            : [];
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverHomebrewAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("brew", ["list", "--versions", "dotnet-sdk"], cancellationToken);
        var version = ParsePackageVersionLine(result.Stdout, "dotnet-sdk");
        return result.ExitCode == 0 && version is not null
            ? [Installed(version, "brew:dotnet-sdk")]
            : [];
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverChocolateyAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("choco", ["list", "--local-only", "--exact", "dotnet-sdk"], cancellationToken);
        var version = ParsePackageVersionLine(result.Stdout, "dotnet-sdk");
        return result.ExitCode == 0 && version is not null
            ? [Installed(version, "choco:dotnet-sdk")]
            : [];
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverWingetAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("winget", ["list", "--id", "Microsoft.DotNet.SDK.10", "--exact"], cancellationToken);
        var version = ParseWingetVersion(result.Stdout, "Microsoft.DotNet.SDK.10");
        return result.ExitCode == 0 && version is not null
            ? [Installed(version, "winget:Microsoft.DotNet.SDK.10")]
            : [];
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

    private static CapabilityInstalledVersion Installed(string version, string location)
        => new("dotnet", version, location, CapabilityVersionSource.System, IsManaged: false);

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

    private sealed class StringTupleComparer : IEqualityComparer<(string, string, string)>
    {
        public static StringTupleComparer Instance { get; } = new();

        public bool Equals((string, string, string) x, (string, string, string) y)
            => string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.Item3, y.Item3, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string, string, string) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item3));
    }
}
