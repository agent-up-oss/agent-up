using System.Collections.Generic;

namespace AgentUp.Verification.Tests.Support;

/// <summary>
/// The shared domain vocabulary for verification tests: one realistic repository shape,
/// named once, reused everywhere.
/// </summary>
/// <remarks>
/// Tests refer to these names rather than inventing their own path and id strings, so a
/// reader can tell at a glance whether two tests are talking about the same thing. Where a
/// test needs something specific, it injects that one value through a builder instead of
/// restating the whole world.
/// </remarks>
internal static class VerificationDomain
{
    public const string ServerSource = "AgentUp.Server/Features/Git/Services/GitChangesService.cs";
    public const string ServerTest = "AgentUp.Server.Tests/Features/Git/Unit/GitChangesServiceTests.cs";
    public const string SharedPolicySource = "AgentUp.CommitPolicy/Features/CommitPolicy/Providers/CommitPolicyProvider.cs";
    public const string MobileSource = "AgentUp.Mobile/src/features/servers/providers/ServerUrlProvider.ts";
    public const string PackagingSource = "packaging/linux/agent-up.service";
    public const string DocumentationSource = "docs/developer-guide/testing.md";
    public const string UnmappedSource = "Experiments/scratch/Prototype.cs";

    public const string ArchitectureCheck = "architecture";
    public const string ServerUnitCheck = "server-unit";
    public const string MobileCheck = "mobile";
    public const string LinuxSmokeCheck = "linux-smoke";
    public const string MacOsSmokeCheck = "macos-smoke";

    public const string Linux = "linux";
    public const string MacOs = "macos";

    /// <summary>
    /// A configuration shaped like the real repository: an always-on architecture check, a
    /// server check with a dependency closure, a mobile check, a Linux-only smoke check,
    /// a CI-only macOS smoke check, and an explicit opt-out for documentation.
    /// </summary>
    public static VerificationConfigurationBuilder Configuration()
        => new VerificationConfigurationBuilder()
            .WithAlways(ArchitectureCheck)
            .WithCheck(new CheckBuilder(ArchitectureCheck).WithCommand("dotnet test AgentUp.Architecture.Tests"))
            .WithCheck(new CheckBuilder(ServerUnitCheck)
                .WithCommand("dotnet test AgentUp.Server.Tests")
                .WithInputs("AgentUp.Server", "AgentUp.Server.Tests", "AgentUp.CommitPolicy"))
            .WithCheck(new CheckBuilder(MobileCheck)
                .WithCommand("npm test")
                .WithWorkingDirectory("AgentUp.Mobile")
                .WithInputs("AgentUp.Mobile"))
            .WithCheck(new CheckBuilder(LinuxSmokeCheck)
                .WithCommand("./.github/scripts/smoke-package.sh ubuntu linux-x64 release-artifacts")
                .WithPlatforms(Linux))
            .WithCheck(new CheckBuilder(MacOsSmokeCheck)
                .WithCommand("./.github/scripts/smoke-package.sh macos osx-arm64 release-artifacts")
                .WithPlatforms(MacOs)
                .WithCiOnly())
            .WithPathRule("AgentUp.Server/**", ServerUnitCheck)
            .WithPathRule("AgentUp.Server.Tests/**", ServerUnitCheck)
            .WithPathRule("AgentUp.CommitPolicy/**", ServerUnitCheck)
            .WithPathRule("AgentUp.Mobile/**", MobileCheck)
            .WithPathRule("packaging/**", LinuxSmokeCheck, MacOsSmokeCheck)
            .WithPathRule("docs/**");

    public static IReadOnlyList<string> AllPaths =>
    [
        ServerSource,
        ServerTest,
        SharedPolicySource,
        MobileSource,
        PackagingSource,
        DocumentationSource
    ];
}
