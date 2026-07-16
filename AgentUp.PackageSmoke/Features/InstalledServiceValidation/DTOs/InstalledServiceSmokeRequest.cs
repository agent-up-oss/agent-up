using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;

public sealed record InstalledServiceSmokeRequest(
    string Platform,
    string RuntimeId,
    string ArtifactDirectory,
    string WorkDirectory,
    string PrimaryServerUrl = "http://127.0.0.1:5000",
    string FallbackServerUrl = "http://localhost:5000");
