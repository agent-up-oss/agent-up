namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

public sealed partial record SmokeProductConfig
{
    public static readonly SmokeProductConfig AgentUp = new(
        ServiceName: "agent-up-server",
        CliShimName: "agent-up",
        ArtifactBaseName: "agent-up",
        DisplayName: "Agent-Up",
        InstallDirName: "Agent-Up");
}
