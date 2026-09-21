using AgentUp.AUDebug.Features.Test.DTOs;
using AgentUp.AUDebug.Features.Test.Interfaces;

namespace AgentUp.AUDebug.Features.Test.Providers;

public sealed class DebugTestSuiteCatalog : IDebugTestSuiteCatalog
{
    public const string All = "all";

    private static readonly DebugTestSuiteDto[] Suites =
    [
        Suite(
            "design-system",
            "Design-system compiler and contract tests",
            Npm("AgentUp.DesignSystem", "run", "build"),
            Npm("AgentUp.DesignSystem")),
        Suite("desktop", "Desktop tests", Dotnet("AgentUp.Desktop.Tests")),
        Suite(
            "mobile",
            "Mobile typecheck, tests, web export, and browser-SSO example",
            Npm("AgentUp.Mobile", "run", "typecheck"),
            Npm("AgentUp.Mobile"),
            Npm("AgentUp.Mobile", "run", "build:web"),
            Npm("Examples/browser-sso")),
        Suite("au-debug", "AUDebug tests", Dotnet("AgentUp.AUDebug.Tests")),
        Suite("architecture", "Architecture tests", Dotnet("AgentUp.Architecture.Tests")),
    ];

    private static readonly DebugTestSuiteDto[] Builds =
    [
        Suite("design-system", "Design-system generated bindings", Npm("AgentUp.DesignSystem", "run", "build")),
        Suite(
            "mobile",
            "Mobile typecheck and web export",
            Npm("AgentUp.Mobile", "run", "typecheck"),
            Npm("AgentUp.Mobile", "run", "build:web")),
    ];

    public IReadOnlyList<string> SuiteIds { get; } = Suites.Select(suite => suite.Id).ToArray();
    public IReadOnlyList<string> BuildIds { get; } = Builds.Select(suite => suite.Id).ToArray();

    public bool TryResolve(string suite, out IReadOnlyList<DebugTestSuiteDto> suites, out string? error)
        => TryResolve(suite, Suites, SuiteIds, "test suite", out suites, out error);

    public bool TryResolveBuild(string target, out IReadOnlyList<DebugTestSuiteDto> suites, out string? error)
        => TryResolve(target, Builds, BuildIds, "build target", out suites, out error);

    private static bool TryResolve(
        string id,
        DebugTestSuiteDto[] all,
        IReadOnlyList<string> known,
        string kind,
        out IReadOnlyList<DebugTestSuiteDto> suites,
        out string? error)
    {
        var key = id.Trim();
        if (key.Length == 0 || string.Equals(key, All, StringComparison.Ordinal))
        {
            suites = all;
            error = null;
            return true;
        }

        var match = all.FirstOrDefault(item => string.Equals(item.Id, key, StringComparison.Ordinal));
        if (match is null)
        {
            suites = [];
            error = $"Error: unknown {kind} '{id}'. Choose {All} or one of: {string.Join(", ", known)}.";
            return false;
        }

        suites = [match];
        error = null;
        return true;
    }

    private static DebugTestSuiteDto Suite(string id, string title, params DebugTestStepDto[] steps)
        => new(id, title, steps);

    private static DebugTestStepDto Dotnet(string project)
        => new("dotnet", ["test", $"{project}/{project}.csproj", "--nologo"], ".");

    private static DebugTestStepDto Npm(string directory, params string[] arguments)
        => new("npm", arguments.Length == 0 ? ["test"] : arguments, directory);
}
