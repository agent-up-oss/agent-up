namespace AgentUp.InstallerConfig;

public static class AgentUpProduct
{
    public const string Name = "Agent-Up";
    public const string Slug = "agent-up";
    public const string EnvironmentPrefix = "AGENTUP";
    public const string WindowsUpgradeCode = "5E8FB224-E5E3-4D48-8B62-2F50D521CBB0";
    public const string FakeInstallerVariable = "AGENTUP_INSTALLER_FAKE";
    public const string PayloadRootVariable = "AGENTUP_INSTALLER_PAYLOAD_ROOT";
    public const string NixOsLookupOnlyVariable = "AGENTUP_INSTALLER_NIXOS_LOOKUP_ONLY";
}
