namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

public sealed record SmokeProductConfig(
    string ServiceName,
    string CliShimName,
    string ArtifactBaseName,
    string DisplayName,
    string InstallDirName,
    string WorkspaceConfigFileName = "agent-up.json")
{
    public static readonly SmokeProductConfig AgentUp = new(
        ServiceName: "agent-up-server",
        CliShimName: "agent-up",
        ArtifactBaseName: "agent-up",
        DisplayName: "Agent-Up",
        InstallDirName: "Agent-Up");
}
