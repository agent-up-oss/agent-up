using AgentUp.InstallerConfig;
using AgentUp.Packaging.Shared.Factories;

return await new PackagingServiceRegistry(
    AgentUpProduct.Name,
    AgentUpProduct.Slug,
    AgentUpProduct.EnvironmentPrefix,
    manufacturer: AgentUpProduct.Name,
    windowsUpgradeCode: AgentUpProduct.WindowsUpgradeCode)
    .PackageCommands.ExecuteAsync(args);
