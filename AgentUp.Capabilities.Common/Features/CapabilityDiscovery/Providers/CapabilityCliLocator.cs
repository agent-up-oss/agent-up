using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;

namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

public sealed class CapabilityCliLocator
{
    private readonly ICapabilityCommandRunner _commands;
    private readonly ICapabilitySearchPathProvider _searchPaths;
    private readonly ICapabilityExecutableProbe _executables;
    private readonly string _platform;

    public CapabilityCliLocator()
        : this(
            new ProcessCapabilityCommandRunner(),
            new CapabilitySearchPathProvider(),
            new CapabilityExecutableProbe(),
            CapabilitySearchPathProvider.CurrentPlatform())
    {
    }

    public CapabilityCliLocator(
        ICapabilityCommandRunner commands,
        ICapabilitySearchPathProvider searchPaths,
        ICapabilityExecutableProbe executables,
        string platform)
    {
        _commands = commands;
        _searchPaths = searchPaths;
        _executables = executables;
        _platform = platform;
    }

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(
        string capabilityId,
        IReadOnlyList<CapabilityCliCandidate> candidates,
        IReadOnlyList<CapabilityPackageProbe> packageProbes,
        CancellationToken cancellationToken)
    {
        var discovered = new List<CapabilityInstalledVersion>();
        discovered.AddRange(await DiscoverCliAsync(capabilityId, candidates, cancellationToken));
        discovered.AddRange(DiscoverWellKnown(capabilityId, candidates));
        discovered.AddRange(await DiscoverPackageManagersAsync(capabilityId, packageProbes, cancellationToken));
        return Deduplicate(discovered);
    }

    public CapabilityCliLaunch ResolveLaunch(
        IReadOnlyList<CapabilityCliCandidate> candidates,
        IReadOnlyList<CapabilityInstalledVersion> installedVersions)
    {
        var preferred = installedVersions
            .OrderByDescending(version => Path.IsPathRooted(version.Location))
            .Select(version => Match(candidates, version.Location))
            .FirstOrDefault(match => match is not null);

        return preferred ?? (candidates.Count == 0
            ? new CapabilityCliLaunch("", [])
            : new CapabilityCliLaunch(candidates[0].FileName, candidates[0].LaunchArguments));
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverCliAsync(
        string capabilityId,
        IReadOnlyList<CapabilityCliCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var discovered = new List<CapabilityInstalledVersion>();
        foreach (var candidate in candidates)
        {
            var result = await _commands.RunAsync(candidate.FileName, candidate.VersionArguments, cancellationToken);
            var version = ParseVersion(result.Stdout);
            if (result.ExitCode == 0 && version is not null)
                discovered.Add(Installed(capabilityId, version, candidate.FileName));
        }

        return discovered;
    }

    private IReadOnlyList<CapabilityInstalledVersion> DiscoverWellKnown(
        string capabilityId,
        IReadOnlyList<CapabilityCliCandidate> candidates)
    {
        var discovered = new List<CapabilityInstalledVersion>();
        foreach (var directory in _searchPaths.Directories())
        {
            foreach (var candidate in candidates)
            {
                foreach (var fileName in FileNames(candidate.FileName))
                {
                    var path = Path.Join(directory, fileName);
                    if (_executables.IsExecutable(path))
                        discovered.Add(Installed(capabilityId, "unknown", path));
                }
            }
        }

        return discovered;
    }

    private async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverPackageManagersAsync(
        string capabilityId,
        IReadOnlyList<CapabilityPackageProbe> packageProbes,
        CancellationToken cancellationToken)
    {
        var discovered = new List<CapabilityInstalledVersion>();
        foreach (var probe in packageProbes.Where(probe => probe.Platform == _platform))
        {
            var result = await _commands.RunAsync(probe.FileName, probe.Arguments, cancellationToken);
            var version = probe.FileName == "winget"
                ? ParseWingetVersion(result.Stdout, probe.PackageName)
                : ParsePackageVersionLine(result.Stdout, probe.PackageName);
            if (result.ExitCode == 0 && version is not null)
                discovered.Add(Installed(capabilityId, version, probe.LocationLabel));
        }

        return discovered;
    }

    private static CapabilityCliLaunch? Match(IReadOnlyList<CapabilityCliCandidate> candidates, string location)
    {
        var candidate = candidates.FirstOrDefault(item => NamesMatch(location, item.FileName));
        return candidate is null ? null : new CapabilityCliLaunch(location, candidate.LaunchArguments);
    }

    private static bool NamesMatch(string location, string candidateFileName)
    {
        var fileName = StripExe(Path.GetFileName(location.AsSpan()).ToString());
        return fileName.Equals(StripExe(candidateFileName), StringComparison.OrdinalIgnoreCase);
    }

    private static string StripExe(string fileName) =>
        fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? fileName[..^4] : fileName;

    private static IReadOnlyList<string> FileNames(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
            name = fileName;
        if (OperatingSystem.IsWindows() && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return [name, name + ".exe"];
        return [name];
    }

    private static string? ParseVersion(string output)
    {
        var token = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim().TrimStart('v', 'V'))
                .FirstOrDefault(part => part.Any(char.IsDigit)))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static string? ParsePackageVersionLine(string output, string packageName)
        => output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(packageName + " ", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[(packageName.Length + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version));

    private static string? ParseWingetVersion(string output, string packageId)
        => output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(token => token.Equals(packageId, StringComparison.OrdinalIgnoreCase)))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault())
            .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version) && char.IsDigit(version[0]));

    private static CapabilityInstalledVersion Installed(string capabilityId, string version, string location)
        => new(capabilityId, version, location, CapabilityVersionSource.System, IsManaged: false);

    private static IReadOnlyList<CapabilityInstalledVersion> Deduplicate(List<CapabilityInstalledVersion> versions)
        => versions
            .GroupBy(version => (version.CapabilityId, version.Version, version.Location), StringComparerTuple.Instance)
            .Select(group => group.First())
            .ToList();
}
