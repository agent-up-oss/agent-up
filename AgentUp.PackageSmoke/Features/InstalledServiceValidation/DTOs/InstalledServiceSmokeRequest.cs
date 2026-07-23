using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;

public sealed record InstalledServiceSmokeRequest(
    string Platform,
    string RuntimeId,
    string ArtifactDirectory,
    string WorkDirectory,
    string PrimaryServerUrl = "http://127.0.0.1:5000",
    string FallbackServerUrl = "http://localhost:5000",
    SmokeProductConfig? ProductConfig = null,
    string SystemRoot = "/")
{
    public SmokeProductConfig Product => ProductConfig ?? SmokeProductConfig.AgentUp;
}
