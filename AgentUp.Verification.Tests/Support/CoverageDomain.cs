namespace AgentUp.Verification.Tests.Support;

/// <summary>
/// Shared vocabulary for coverage tests: one realistic changed-file shape, named once.
/// </summary>
internal static class CoverageDomain
{
    public const string ServerSource = "AgentUp.Server/Features/Git/Services/GitChangesService.cs";
    public const string CliSource = "AgentUp.CLI/Features/Commits/Services/CommitsService.cs";
    public const string CompositionSource = "AgentUp.Server/Composition/ServiceRegistration.cs";
    public const string DocumentationSource = "docs/developer-guide/testing.md";

    public const string IncludeGlob = "AgentUp.*/Features/**/*.cs";
    public const string ExcludeGlob = "AgentUp.*/Composition/**";

    public static CoverageConfigurationBuilder Configuration()
        => new CoverageConfigurationBuilder()
            .WithMinimum(90d)
            .WithInclude(IncludeGlob)
            .WithExclude(ExcludeGlob);
}
