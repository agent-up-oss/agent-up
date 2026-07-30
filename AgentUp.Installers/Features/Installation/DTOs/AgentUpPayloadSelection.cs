namespace AgentUp.Installers.Features.Installation.DTOs;

public sealed partial record PayloadSelection
{
    private const string AgentUpProductName = "Agent-Up";

    public static PayloadSelection Bundled(Version version)
        => Bundled(AgentUpProductName, version);

    public static PayloadSelection Online(Version version, string downloadUrl)
        => Online(AgentUpProductName, version, downloadUrl);
}
