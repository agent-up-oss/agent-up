using LocalInstaller.Core.Shared.Models;

namespace AgentUp.InstallerApp.Composition;

public sealed class AgentUpInstallerAppManifest : LocalInstallerInstallerAppManifest
{
    public override string Id => "agent-up-installer";
    public override string DisplayName => "Installer";
    public override string Description => "Installer and maintenance app.";
    public override string ExecutableName => "AgentUp.InstallerApp";
    public override string SourceProjectPath => "AgentUp.InstallerApp/AgentUp.InstallerApp.csproj";
    public override string PayloadDirectoryName => "installer";
}
