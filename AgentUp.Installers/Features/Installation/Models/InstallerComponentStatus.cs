namespace AgentUp.Installers.Features.Installation.Models;

public enum InstallerComponentStatusKind
{
    NotInstalled,
    Installing,
    Installed,
    UpdateAvailable,
    Uninstalling,
    Failed
}

public sealed record InstallerComponentStatus(
    ProductComponent Component,
    InstallerComponentStatusKind Kind,
    Version? InstalledVersion = null,
    Version? AvailableVersion = null,
    string? Message = null)
{
    public bool IsInstalled => Kind is InstallerComponentStatusKind.Installed or InstallerComponentStatusKind.UpdateAvailable;
}
