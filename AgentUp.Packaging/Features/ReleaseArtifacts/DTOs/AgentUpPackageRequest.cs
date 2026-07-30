namespace AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

public sealed partial record PackageRequest
{
    public PackageRequest(
        string repositoryRoot,
        string platform,
        string runtimeId,
        string version,
        string outputDirectory,
        string configuration)
        : this(
            repositoryRoot,
            platform,
            runtimeId,
            version,
            outputDirectory,
            configuration,
            null,
            PackageProductManifest.AgentUp())
    {
    }

    public PackageRequest(
        string repositoryRoot,
        string platform,
        string runtimeId,
        string version,
        string outputDirectory,
        string configuration,
        PackageProductManifest productManifest)
        : this(
            repositoryRoot,
            platform,
            runtimeId,
            version,
            outputDirectory,
            configuration,
            null,
            productManifest)
    {
        PackageProductManifest.Validate(productManifest);
    }

    public PackageRequest(
        string repositoryRoot,
        string platform,
        string runtimeId,
        string version,
        string outputDirectory,
        string configuration,
        string? payloadRoot)
        : this(
            repositoryRoot,
            platform,
            runtimeId,
            version,
            outputDirectory,
            configuration,
            payloadRoot,
            PackageProductManifest.AgentUp())
    {
    }
}
