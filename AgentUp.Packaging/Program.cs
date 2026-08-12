using AgentUp.CLI;
using AgentUp.CLI.Composition;
using AgentUp.Desktop;
using AgentUp.Desktop.Composition;
using AgentUp.InstallerApp.Composition;
using AgentUp.InstallerConfig;
using LocalInstaller.App;
using LocalInstaller.App.Composition;
using AgentUp.Server;
using AgentUp.Server.Composition;
using AgentUp.Tray;
using AgentUp.Tray.Composition;
using LocalInstaller.Packaging.Composition;

return await LocalInstallerPackager.Create(args)
    .UseProductManifest<AgentUpProductManifest>()
    .InstallerApplication<AgentUpInstallerAppManifest>()
    .InstallerOptionCli<AgentUpCliManifest>()
    .InstallerOptionServer<AgentUpServerManifest>()
    .InstallerOptionDesktop<AgentUpDesktopManifest>()
    .InstallerOptionTray<AgentUpTrayManifest>()
    .Windows(options => options
        .WithUpgradeCode(AgentUpProduct.WindowsUpgradeCode)
        .WithCliShimName(AgentUpProduct.Slug + ".cmd"))
    .RunAsync();
