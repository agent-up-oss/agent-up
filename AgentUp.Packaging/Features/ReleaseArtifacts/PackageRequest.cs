namespace AgentUp.Packaging.Features.ReleaseArtifacts;

public sealed record PackageRequest(
    string RepositoryRoot,
    string Platform,
    string RuntimeId,
    string Version,
    string OutputDirectory,
    string Configuration)
{
    public string NormalizedVersion => Version.TrimStart('v', 'V');
    public string StageDirectory => Path.Combine(RepositoryRoot, "artifacts", "stage", $"{Platform}-{RuntimeId}");
    public string OutputRoot => Path.Combine(RepositoryRoot, OutputDirectory);
}
