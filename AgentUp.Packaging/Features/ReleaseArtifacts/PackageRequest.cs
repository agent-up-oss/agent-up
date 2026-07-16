namespace AgentUp.Packaging.Features.ReleaseArtifacts;

public sealed record PackageRequest(
    string RepositoryRoot,
    string Platform,
    string RuntimeId,
    string Version,
    string OutputDirectory,
    string Configuration,
    string? PayloadRoot = null)
{
    public string NormalizedVersion => Version.TrimStart('v', 'V');
    public string StageDirectory => Path.Join(RepositoryRoot, "artifacts", "stage", $"{Platform}-{RuntimeId}");
    public string OutputRoot => Path.Join(RepositoryRoot, OutputDirectory);
    public string? DesktopPayloadDirectory => PayloadRoot is null ? null : Path.Join(PayloadRoot, "desktop");
    public string? ServerPayloadDirectory => PayloadRoot is null ? null : Path.Join(PayloadRoot, "server");
    public string? CliPayloadDirectory => PayloadRoot is null ? null : Path.Join(PayloadRoot, "cli");
}
