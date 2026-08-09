using LocalInstaller.Core.Shared.Models;

namespace AgentUp.Tray.Composition;

public sealed class AgentUpTrayManifest : LocalInstallerTrayManifest
{
    public override string Id => "agent-up-tray";
    public override string DisplayName => "Tray";
    public override string Description => "Notification area app.";
    public override string ExecutableName => "AgentUp.Tray";
    public override string SourceProjectPath => "AgentUp.Tray/AgentUp.Tray.csproj";
    public override string PayloadDirectoryName => "tray";
}
