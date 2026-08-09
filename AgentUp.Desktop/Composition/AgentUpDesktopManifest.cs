using LocalInstaller.Core.Shared.Models;

namespace AgentUp.Desktop.Composition;

public sealed class AgentUpDesktopManifest : LocalInstallerDesktopManifest
{
    public override string Id => "agent-up-desktop";
    public override string DisplayName => "Desktop";
    public override string Description => "Desktop app.";
    public override string ExecutableName => "AgentUp.Desktop";
    public override string SourceProjectPath => "AgentUp.Desktop/AgentUp.Desktop.csproj";
    public override string PayloadDirectoryName => "desktop";
}
