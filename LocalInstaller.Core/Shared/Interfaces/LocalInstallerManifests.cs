namespace LocalInstaller.Core.Shared.Models;

public enum LocalInstallerArtifactTarget
{
    Desktop,
    Server,
    Cli,
    Tray,
    InstallerApp
}

public sealed record LocalInstallerArtifactDescriptor(
    string Id,
    string DisplayName,
    string Description,
    string ExecutableName,
    string SourceProjectPath,
    string PayloadDirectoryName,
    LocalInstallerArtifactTarget Target);

public abstract class LocalInstallerProductManifest
{
    public abstract string ProductName { get; }
    public abstract string Slug { get; }
    public abstract string EnvironmentPrefix { get; }
    public virtual string? Manufacturer => ProductName;
}

public abstract class LocalInstallerArtifactManifest
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public virtual string Description => "";
    public abstract string ExecutableName { get; }
    public abstract string SourceProjectPath { get; }
    public virtual string PayloadDirectoryName => Id;
    public abstract LocalInstallerArtifactTarget Target { get; }

    public LocalInstallerArtifactDescriptor ToDescriptor()
        => new(Id, DisplayName, Description, ExecutableName, SourceProjectPath, PayloadDirectoryName, Target);
}

public abstract class LocalInstallerCliManifest : LocalInstallerArtifactManifest
{
    public sealed override LocalInstallerArtifactTarget Target => LocalInstallerArtifactTarget.Cli;
}

public abstract class LocalInstallerServerManifest : LocalInstallerArtifactManifest
{
    public sealed override LocalInstallerArtifactTarget Target => LocalInstallerArtifactTarget.Server;
    public virtual string ServiceName => $"{Id}-server";
    public virtual string ServerUrl => "http://127.0.0.1:5000";
}

public abstract class LocalInstallerDesktopManifest : LocalInstallerArtifactManifest
{
    public sealed override LocalInstallerArtifactTarget Target => LocalInstallerArtifactTarget.Desktop;
}

public abstract class LocalInstallerTrayManifest : LocalInstallerArtifactManifest
{
    public sealed override LocalInstallerArtifactTarget Target => LocalInstallerArtifactTarget.Tray;
}

public abstract class LocalInstallerInstallerAppManifest : LocalInstallerArtifactManifest
{
    public sealed override LocalInstallerArtifactTarget Target => LocalInstallerArtifactTarget.InstallerApp;
}
