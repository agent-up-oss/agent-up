using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;

public sealed class MacOsTrayAutoStartProvider
{
    public string LaunchAgentPath(SmokeProductConfig product)
        => Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents", $"dev.{product.CliShimName}.tray.plist");
}
