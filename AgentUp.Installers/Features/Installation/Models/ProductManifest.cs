namespace AgentUp.Installers.Features.Installation.Models;

public sealed record ProductManifest(
    string ProductName,
    string Slug,
    string EnvironmentPrefix)
{
    public static ProductManifest AgentUp()
        => new("Agent-Up", "agent-up", "AGENTUP");

    public string ServiceName => $"{Slug}-server";
    public string CliCommandName => Slug;
    public string PayloadRootVariable => $"{EnvironmentPrefix}_INSTALLER_PAYLOAD_ROOT";

    public string DefaultInstallRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (string.IsNullOrWhiteSpace(programFiles))
                programFiles = @"C:\Program Files";
            return System.IO.Path.Combine(programFiles, ProductName);
        }

        if (OperatingSystem.IsMacOS())
            return $"/Applications/{ProductName}.app";

        return $"/opt/{Slug}";
    }
}
