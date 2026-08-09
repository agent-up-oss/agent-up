using LocalInstaller.Smoke.Features.InstalledServiceValidation.Models;

namespace LocalInstaller.Smoke.Features.InstalledServiceValidation.Providers;

public sealed class MacOsTrayAutoStartProvider
{
    public string LaunchAgentPath(SmokeProductConfig product)
        => Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents", $"dev.{product.CliShimName}.tray.plist");
}
