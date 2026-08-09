using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation.Provider;

[TestFixture]
public sealed class TrayAutoStartProviderTests
{
    [Test]
    public void WindowsValidationCommand_comparesRunValueToExpectedTrayExecutable()
    {
        var product = AgentUpSmokeTestManifests.Product();
        var trayExecutable = Path.Join("C:", "Program Files", "Agent-Up", "tray", "AgentUp.Tray.exe");

        var command = new WindowsTrayAutoStartSmokeProvider().ValidationCommand(product, trayExecutable);

        Assert.That(command.FileName, Is.EqualTo("powershell.exe"));
        Assert.That(command.Arguments, Is.EqualTo(new[]
        {
            "-NoProfile",
            "-Command",
            "$name = $env:AGENTUP_TRAY_AUTOSTART_NAME; $expected = $env:AGENTUP_TRAY_AUTOSTART_VALUE; $val = Get-ItemPropertyValue -Path 'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name $name -ErrorAction SilentlyContinue; if (-not [string]::Equals($val, $expected, [System.StringComparison]::OrdinalIgnoreCase)) { throw \"$name tray autostart registry entry missing or incorrect\" }"
        }));
        Assert.That(command.Environment, Is.Not.Null);
        Assert.That(command.Environment!["AGENTUP_TRAY_AUTOSTART_NAME"], Is.EqualTo("Agent-Up"));
        Assert.That(command.Environment!["AGENTUP_TRAY_AUTOSTART_VALUE"], Is.EqualTo($"\"{trayExecutable}\""));
    }

    [Test]
    public void MacOsLaunchAgentPath_usesCurrentUserLaunchAgentsAndProductShim()
    {
        var product = AgentUpSmokeTestManifests.Product();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var path = new MacOsTrayAutoStartProvider().LaunchAgentPath(product);

        Assert.That(path, Is.EqualTo(Path.Join(home, "Library", "LaunchAgents", "dev.agent-up.tray.plist")));
    }
}
