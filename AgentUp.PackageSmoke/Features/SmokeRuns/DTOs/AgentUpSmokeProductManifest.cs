namespace AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;

public sealed partial record SmokeProductManifest
{
    public static readonly SmokeProductManifest AgentUp = new(
        ServiceName: "agent-up-server",
        CliShimName: "agent-up",
        ArtifactBaseName: "agent-up",
        DisplayName: "Agent-Up",
        InstallDirName: "Agent-Up");
}
