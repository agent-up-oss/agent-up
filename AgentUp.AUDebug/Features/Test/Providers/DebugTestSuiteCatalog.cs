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
        Suite("mobile", "Mobile tests", Npm("AgentUp.Mobile")),
        Suite("au-debug", "AUDebug tests", Dotnet("AgentUp.AUDebug.Tests")),
        Suite("architecture", "Architecture tests", Dotnet("AgentUp.Architecture.Tests")),
    ];

    public IReadOnlyList<string> SuiteIds { get; } = Suites.Select(suite => suite.Id).ToArray();

    public bool TryResolve(string suite, out IReadOnlyList<DebugTestSuiteDto> suites, out string? error)
    {
        var id = suite.Trim();
        if (id.Length == 0 || string.Equals(id, All, StringComparison.Ordinal))
        {
            suites = Suites;
            error = null;
            return true;
        }

        var match = Suites.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        if (match is null)
        {
            suites = [];
            error = $"Error: unknown test suite '{id}'. Choose {All} or one of: {string.Join(", ", SuiteIds)}.";
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
