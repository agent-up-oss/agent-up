using LocalInstaller.Core.Shared.Models;

namespace LocalInstaller.Sample.Tray;

public sealed class SampleTrayManifest : LocalInstallerTrayManifest
{
    public override string Id => "sample-tray";
    public override string DisplayName => "Tray";
    public override string Description => "Notification area app.";
    public override string ExecutableName => "LocalInstaller.Sample.Tray";
    public override string SourceProjectPath => "LocalInstaller.Sample.Tray/LocalInstaller.Sample.Tray.csproj";
    public override string PayloadDirectoryName => Id;
}
