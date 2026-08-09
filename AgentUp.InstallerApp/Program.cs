using AgentUp.InstallerConfig;
using AgentUp.InstallerApp;
using AgentUp.InstallerApp.Composition;

return await LocalInstallerApp.Create(args)
    .Product(AgentUpProduct.Name, AgentUpProduct.Slug, AgentUpProduct.EnvironmentPrefix)
    .Component("cli", "CLI", "Command-line app.")
    .Component("server", "Server", "Local service app.")
    .Component("desktop", "Desktop", "Desktop app.")
    .Component("tray", "Tray", "Notification area app.")
    .Manufacturer(AgentUpProduct.Name)
    .UpgradeCode(AgentUpProduct.WindowsUpgradeCode)
    .FakeInstallerVariable(AgentUpProduct.FakeInstallerVariable)
    .NixOsLookupOnlyVariable(AgentUpProduct.NixOsLookupOnlyVariable)
    .RunAsync<App>();
