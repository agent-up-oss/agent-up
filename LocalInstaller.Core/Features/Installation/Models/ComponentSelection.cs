namespace LocalInstaller.Core.Features.Installation.Models;

[Flags]
public enum InstallerComponent
{
    None = 0,
    Server = 1,
    Cli = 2,
    Desktop = 4,
    NativeService = 8,
    RuntimeDependencies = 16,
    Tray = 32
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
            | InstallerComponent.Tray
            | InstallerComponent.RuntimeDependencies,
            new InstallLocation(rootDirectory));

    public static InstallerComponent FromComponents(IReadOnlyList<ProductComponent> components)
    {
        if (components.Count == 0)
            return CreateDefault("", new Version(0, 0, 0), "").Components;

        var selected = InstallerComponent.RuntimeDependencies;
        foreach (var component in components)
        {
            selected |= TargetFor(component) switch
            {
                InstallerComponentTarget.Server => InstallerComponent.Server,
                InstallerComponentTarget.Cli => InstallerComponent.Cli,
                InstallerComponentTarget.Desktop => InstallerComponent.Desktop,
                InstallerComponentTarget.Tray => InstallerComponent.Tray,
                _ => InstallerComponent.None
            };
        }

        return selected;
    }

    private static InstallerComponentTarget? TargetFor(ProductComponent component)
        => component.Target
           ?? (Enum.TryParse<InstallerComponentTarget>(component.Id, ignoreCase: true, out var target) ? target : null);
}
