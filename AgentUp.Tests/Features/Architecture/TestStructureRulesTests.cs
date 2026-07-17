namespace AgentUp.Tests.Features.Architecture;

[TestFixture]
public sealed class TestStructureRulesTests
{
    private static readonly string[] ForbiddenUnitTestTokens =
    [
        "File.",
        "Directory.",
        "Path.GetTempPath",
        "Process.Start",
        "new ProcessStartInfo",
        "Directory.SetCurrentDirectory",
        "Environment.SetEnvironmentVariable",
        "TcpListener",
        "TcpClient",
        "Socket"
    ];

    [Test]
    public void Unit_tests_do_not_use_real_io_process_socket_or_environment_mutation()
    {
        var repositoryRoot = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = Directory.EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => IsUnitTest(path, repositoryRoot))
            .SelectMany(path => Violations(path, repositoryRoot))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Tests that use real filesystem, process, socket, current-directory, or environment mutation APIs must live in Repository, Provider, TerminalIntegration, HTTP, Headless, or E2E folders, not Unit.");
    }

    private static IEnumerable<string> Violations(string path, string repositoryRoot)
    {
        var source = File.ReadAllText(path);
        return ForbiddenUnitTestTokens
            .Where(source.Contains)
            .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}");
    }

    private static bool IsUnitTest(string path, string repositoryRoot)
    {
        var relative = Path.GetRelativePath(repositoryRoot, path);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("Unit") && parts.FirstOrDefault()?.EndsWith(".Tests", StringComparison.Ordinal) == true;
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "agent-up.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
