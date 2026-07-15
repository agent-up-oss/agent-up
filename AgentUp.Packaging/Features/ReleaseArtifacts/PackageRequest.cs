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
    public string StageDirectory => Path.Combine(RepositoryRoot, "artifacts", "stage", $"{Platform}-{RuntimeId}");
    public string OutputRoot => Path.Combine(RepositoryRoot, OutputDirectory);
    public string? DesktopPayloadDirectory => PayloadRoot is null ? null : Path.Combine(PayloadRoot, "desktop");
    public string? ServerPayloadDirectory => PayloadRoot is null ? null : Path.Combine(PayloadRoot, "server");
    public string? CliPayloadDirectory => PayloadRoot is null ? null : Path.Combine(PayloadRoot, "cli");
}
