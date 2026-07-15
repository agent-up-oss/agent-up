using AgentUp.Installers.Features.Components;
using AgentUp.Installers.Features.Payloads;
using AgentUp.Installers.Features.Prerequisites;
using AgentUp.Installers.Features.Validation;

namespace AgentUp.Installers.Features.Flow;

public sealed record InstallerSession(
    string ProductName,
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
    public static InstallerSession CreateDefault(
        string productName,
        Version version,
        string installRoot,
        PayloadSelection payload)
        => new(
            productName,
            version,
            InstallerStep.Welcome,
            LicenseAccepted: false,
            DockerStatus: null,
            Components: ComponentSelection.CreateDefault(productName, version, installRoot).Components,
            Location: new InstallLocation(installRoot),
            ServerUrl: "http://127.0.0.1:5000",
            Payload: payload,
            ValidationReport: null);

    public InstallSummary Summary()
        => new(ProductName, Version, Components, Location);
}
