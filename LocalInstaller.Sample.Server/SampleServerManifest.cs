using LocalInstaller.Core.Shared.Models;
using LocalInstaller.Sample;

namespace LocalInstaller.Sample.Server;

public sealed class SampleServerManifest : LocalInstallerServerManifest
{
    public override string Id => "sample-server";
    public override string DisplayName => "Server";
    public override string Description => "Local service app.";
    public override string ExecutableName => "LocalInstaller.Sample.Server";
    public override string SourceProjectPath => "LocalInstaller.Sample.Server/LocalInstaller.Sample.Server.csproj";
    public override string PayloadDirectoryName => Id;
    public override string ServiceName => SampleProduct.Slug + "-server";
    public override string ServerUrl => SampleProduct.ServerUrl;
}
