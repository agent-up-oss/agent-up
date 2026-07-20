namespace AgentUp.Installers.Features.Installation.DTOs;

public enum PayloadSourceKind
{
    Bundled,
    Online
}

public sealed record PayloadSelection(
    PayloadSourceKind Source,
    Version Version,
    string Description,
    string? DownloadUrl = null)
{
    public static PayloadSelection Bundled(Version version)
        => Bundled("Agent-Up", version);

    public static PayloadSelection Bundled(string productName, Version version)
        => new(PayloadSourceKind.Bundled, version, $"Bundled {productName} {version}");

    public static PayloadSelection Online(Version version, string downloadUrl)
        => Online("Agent-Up", version, downloadUrl);

    public static PayloadSelection Online(string productName, Version version, string downloadUrl)
        => new(PayloadSourceKind.Online, version, $"Online {productName} {version}", downloadUrl);
}
