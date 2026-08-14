using LocalInstaller.Core.Shared.Models;
using LocalInstaller.Sample;

namespace LocalInstaller.Sample.Cli;

public sealed class SampleCliManifest : LocalInstallerCliManifest
{
    public override string Id => "sample-cli";
    public override string DisplayName => "CLI";
    public override string Description => "Command-line app.";
    public override string ExecutableName => "LocalInstaller.Sample.Cli";
    public override string SourceProjectPath => "LocalInstaller.Sample.Cli/LocalInstaller.Sample.Cli.csproj";
    public override string PayloadDirectoryName => Id;
}

public sealed class SampleProductManifest : LocalInstallerProductManifest
{
    public override string ProductName => SampleProduct.Name;
    public override string Slug => SampleProduct.Slug;
    public override string EnvironmentPrefix => SampleProduct.EnvironmentPrefix;
}
