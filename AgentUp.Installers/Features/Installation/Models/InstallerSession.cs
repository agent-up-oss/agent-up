using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Features.Installation.Models;

public sealed record InstallerSession(
    ProductManifest Manifest,
    Version Version,
    InstallerStep Step,
    bool LicenseAccepted,
    DockerStatus? DockerStatus,
    InstallerComponent Components,
    InstallLocation Location,
    string ServerUrl,
    PayloadSelection Payload,
    ValidationReport? ValidationReport)
{
    public string ProductName => Manifest.ProductName;

    public static InstallerSession CreateDefault(
        ProductManifest manifest,
        Version version,
        string installRoot,
        PayloadSelection payload)
        => new(
            manifest,
            version,
            InstallerStep.Welcome,
            LicenseAccepted: false,
            DockerStatus: null,
            Components: ComponentSelection.CreateDefault(manifest.ProductName, version, installRoot).Components,
            Location: new InstallLocation(installRoot),
            ServerUrl: "http://127.0.0.1:5000",
            Payload: payload,
            ValidationReport: null);

    public InstallSummary Summary()
        => new(ProductName, Version, Components, Location);
}
