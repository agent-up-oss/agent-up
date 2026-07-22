namespace AgentUp.Installers.Features.Installation.Models;

public sealed record ProductManifest(
    string ProductName,
    string Slug,
    string EnvironmentPrefix)
{
    private static readonly ProductManifest AgentUpManifest = new("Agent-Up", "agent-up", "AGENTUP")
    {
        Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
    };

    public IReadOnlyList<ProductComponent> Components { get; init; } = [];
    public string? Manufacturer { get; init; }
    public string? WindowsUpgradeCode { get; init; }

    public static ProductManifest AgentUp()
        => AgentUpManifest;

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
            return System.IO.Path.Join(programFiles, ProductName);
        }

        if (OperatingSystem.IsMacOS())
            return $"/Applications/{ProductName}.app";

        return $"/opt/{Slug}";
    }
}
