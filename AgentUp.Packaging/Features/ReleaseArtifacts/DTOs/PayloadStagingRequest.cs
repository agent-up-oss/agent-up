namespace AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

public sealed record PayloadStagingRequest(
    PackageRequest Package,
    string? InstallerPublishDirectory,
    string DesktopPublishDirectory,
    string ServerPublishDirectory,
    string CliPublishDirectory);
