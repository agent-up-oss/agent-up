using AgentUp.CLI;
using AgentUp.CLI.Composition;
using AgentUp.Desktop;
using AgentUp.Desktop.Composition;
using AgentUp.InstallerConfig;
using AgentUp.InstallerApp;
using AgentUp.InstallerApp.Composition;
using AgentUp.Server;
using AgentUp.Server.Composition;
using AgentUp.Tray;
using AgentUp.Tray.Composition;

return await LocalInstallerApp.Create(args)
    .UseProductManifest<AgentUpProductManifest>()
    .InstallerOptionCli<AgentUpCliManifest>()
    .InstallerOptionServer<AgentUpServerManifest>()
    .InstallerOptionDesktop<AgentUpDesktopManifest>()
    .InstallerOptionTray<AgentUpTrayManifest>()
    .Windows(options => options.WithUpgradeCode(AgentUpProduct.WindowsUpgradeCode))
    .FakeInstallerVariable(AgentUpProduct.FakeInstallerVariable)
    .NixOsLookupOnlyVariable(AgentUpProduct.NixOsLookupOnlyVariable)
    .RunAsync<AgentUp.InstallerApp.App>();
