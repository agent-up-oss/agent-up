namespace LocalInstaller.Core.Features.Installation.DTOs;

public enum PayloadSourceKind
{
    Bundled,
    Online
}

public sealed partial record PayloadSelection(
    PayloadSourceKind Source,
    Version Version,
    string Description,
    string? DownloadUrl = null)
{
    public static PayloadSelection Bundled(string productName, Version version)
        => new(PayloadSourceKind.Bundled, version, $"Bundled {productName} {version}");

    public static PayloadSelection Online(string productName, Version version, string downloadUrl)
        => new(PayloadSourceKind.Online, version, $"Online {productName} {version}", downloadUrl);
}
