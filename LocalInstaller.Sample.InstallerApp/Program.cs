using AgentUp.InstallerApp;
using AgentUp.InstallerApp.Composition;
using LocalInstaller.Sample.Cli;
using LocalInstaller.Sample.Desktop;
using LocalInstaller.Sample.Server;
using LocalInstaller.Sample.Tray;

return await LocalInstallerApp.Create(args)
    .UseProductManifest<SampleProductManifest>()
    .InstallerOptionCli<SampleCliManifest>()
    .InstallerOptionServer<SampleServerManifest>()
    .InstallerOptionDesktop<SampleDesktopManifest>()
    .InstallerOptionTray<SampleTrayManifest>()
    .RunAsync<App>();
