using LocalInstaller.Packaging.Composition;
using LocalInstaller.Sample;
using LocalInstaller.Sample.Cli;
using LocalInstaller.Sample.Desktop;
using LocalInstaller.Sample.InstallerApp;
using LocalInstaller.Sample.Server;
using LocalInstaller.Sample.Tray;

return await LocalInstallerPackager.Create(args)
    .UseProductManifest<SampleProductManifest>()
    .InstallerApplication<SampleInstallerAppManifest>()
    .InstallerOptionCli<SampleCliManifest>()
    .InstallerOptionServer<SampleServerManifest>()
    .InstallerOptionDesktop<SampleDesktopManifest>()
    .InstallerOptionTray<SampleTrayManifest>()
    .Windows(options => options
        .WithUpgradeCode(SampleProduct.UpgradeCode)
        .WithCliShimName(SampleProduct.Slug + ".cmd"))
    .RunAsync();
