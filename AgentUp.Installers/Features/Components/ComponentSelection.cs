namespace AgentUp.Installers.Features.Components;

[Flags]
public enum InstallerComponent
{
    None = 0,
    Server = 1,
    Cli = 2,
    Desktop = 4,
    NativeService = 8,
    RuntimeDependencies = 16
}

public sealed record InstallLocation(string RootDirectory);

public sealed record InstallSummary(
    string ProductName,
    Version Version,
    InstallerComponent Components,
    InstallLocation Location)
{
    public bool Includes(InstallerComponent component)
        => Components.HasFlag(component);
}

public static class ComponentSelection
{
    public static InstallSummary CreateDefault(string productName, Version version, string rootDirectory)
        => new(
            productName,
            version,
            InstallerComponent.Server
            | InstallerComponent.Cli
            | InstallerComponent.Desktop
            | InstallerComponent.NativeService
            | InstallerComponent.RuntimeDependencies,
            new InstallLocation(rootDirectory));
}
