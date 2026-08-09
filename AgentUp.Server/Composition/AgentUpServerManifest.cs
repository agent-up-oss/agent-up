using AgentUp.InstallerConfig;
using LocalInstaller.Core.Shared.Models;

namespace AgentUp.Server.Composition;

public sealed class AgentUpServerManifest : LocalInstallerServerManifest
{
    public override string Id => "agent-up-server";
    public override string DisplayName => "Server";
    public override string Description => "Local service app.";
    public override string ExecutableName => "AgentUp.Server";
    public override string SourceProjectPath => "AgentUp.Server/AgentUp.Server.csproj";
    public override string PayloadDirectoryName => Id;
    public override string ServiceName => AgentUpProduct.Slug + "-server";
}
