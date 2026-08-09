using LocalInstaller.Core.Shared.Models;

namespace LocalInstaller.Sample.InstallerApp;

public sealed class SampleInstallerAppManifest : LocalInstallerInstallerAppManifest
{
    public override string Id => "sample-installer";
    public override string DisplayName => "Installer";
    public override string Description => "Installer and maintenance app.";
    public override string ExecutableName => "LocalInstaller.Sample.InstallerApp";
    public override string SourceProjectPath => "LocalInstaller.Sample.InstallerApp/LocalInstaller.Sample.InstallerApp.csproj";
    public override string PayloadDirectoryName => "installer";
}
