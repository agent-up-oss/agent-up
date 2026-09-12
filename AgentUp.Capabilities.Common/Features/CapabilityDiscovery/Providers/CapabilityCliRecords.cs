namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

public sealed record CapabilityCliCandidate(
    string FileName,
    IReadOnlyList<string> VersionArguments,
    IReadOnlyList<string> LaunchArguments);

public sealed record CapabilityPackageProbe(
    string FileName,
    IReadOnlyList<string> Arguments,
    string LocationLabel,
    string PackageName,
    string Platform);

public sealed record CapabilityCliLaunch(string FileName, IReadOnlyList<string> Arguments);
