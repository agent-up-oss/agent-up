namespace AgentUp.Installers.Features.Payloads.Models;

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
        => new(PayloadSourceKind.Bundled, version, $"Bundled Agent-Up {version}");

    public static PayloadSelection Online(Version version, string downloadUrl)
        => new(PayloadSourceKind.Online, version, $"Online Agent-Up {version}", downloadUrl);
}
