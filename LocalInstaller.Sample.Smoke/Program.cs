using LocalInstaller.Smoke.Composition;
using LocalInstaller.Sample.Cli;
using LocalInstaller.Sample.Desktop;
using LocalInstaller.Sample.InstallerApp;
using LocalInstaller.Sample.Server;
using LocalInstaller.Sample.Tray;

return await LocalInstallerSmoke.Create(args)
    .UseProductManifest<SampleProductManifest>()
    .InstallerApplication<SampleInstallerAppManifest>()
    .InstallerOptionCli<SampleCliManifest>()
    .InstallerOptionServer<SampleServerManifest>()
    .InstallerOptionDesktop<SampleDesktopManifest>()
    .InstallerOptionTray<SampleTrayManifest>()
    .WorkspaceConfigFileName(LocalInstaller.Sample.SampleProduct.WorkspaceConfigFileName)
    .RunAsync(Console.Out, Console.Error);
