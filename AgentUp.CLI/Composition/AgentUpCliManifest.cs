using AgentUp.InstallerConfig;
using LocalInstaller.Core.Shared.Models;

namespace AgentUp.CLI.Composition;

public sealed class AgentUpProductManifest : LocalInstallerProductManifest
{
    public override string ProductName => AgentUpProduct.Name;
    public override string Slug => AgentUpProduct.Slug;
    public override string EnvironmentPrefix => AgentUpProduct.EnvironmentPrefix;
}

public sealed class AgentUpCliManifest : LocalInstallerCliManifest
{
    public override string Id => "agent-up-cli";
    public override string DisplayName => "CLI";
    public override string Description => "Command-line app.";
    public override string ExecutableName => "AgentUp.CLI";
    public override string SourceProjectPath => "AgentUp.CLI/AgentUp.CLI.csproj";
    public override string PayloadDirectoryName => "cli";
}
