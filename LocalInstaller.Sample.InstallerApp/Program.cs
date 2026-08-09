using AgentUp.InstallerApp;
using AgentUp.InstallerApp.Composition;
using LocalInstaller.Sample;

return await LocalInstallerApp.Create(args)
    .Product(SampleProduct.Name, SampleProduct.Slug, SampleProduct.EnvironmentPrefix)
    .Component("cli", "CLI", "Command-line app.")
    .Component("server", "Server", "Local service app.")
    .Component("desktop", "Desktop", "Desktop app.")
    .Component("tray", "Tray", "Notification area app.")
    .Manufacturer(SampleProduct.Name)
    .UpgradeCode(SampleProduct.UpgradeCode)
    .FakeInstallerVariable(SampleProduct.FakeInstallerVariable)
    .NixOsLookupOnlyVariable(SampleProduct.NixOsLookupOnlyVariable)
    .RunAsync<App>();
