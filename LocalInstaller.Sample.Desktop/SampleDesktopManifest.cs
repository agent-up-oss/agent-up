using LocalInstaller.Core.Shared.Models;

namespace LocalInstaller.Sample.Desktop;

public sealed class SampleDesktopManifest : LocalInstallerDesktopManifest
{
    public override string Id => "sample-desktop";
    public override string DisplayName => "Desktop";
    public override string Description => "Desktop app.";
    public override string ExecutableName => "LocalInstaller.Sample.Desktop";
    public override string SourceProjectPath => "LocalInstaller.Sample.Desktop/LocalInstaller.Sample.Desktop.csproj";
    public override string PayloadDirectoryName => Id;
}
