using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Coverage.Providers;

namespace AgentUp.Verification.Tests.Features.Coverage.Provider;

[TestFixture]
public sealed class CoverageConfigurationLoaderTests
{
    private static string WriteRepository(string agentUpJson)
    {
        var root = Path.Join(
            TestContext.CurrentContext.WorkDirectory, "coverage-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.json"), agentUpJson);
        return root;
    }

    [Test]
    public void Load_returnsEmptyWhenThereIsNoCoverageSection()
    {
        var root = WriteRepository("""{ "name": "Agent Up" }""");

        Assert.That(new CoverageConfigurationLoader().Load(root).IsConfigured, Is.False);
    }

    [Test]
    public void Load_readsMinimumReportDirectoryIncludeAndExclude()
    {
        var root = WriteRepository("""
        {
          "coverage": {
            "minimum": 90,
            "reportDirectory": "artifacts/coverage",
            "include": ["AgentUp.Server/**/*.cs"],
            "exclude": ["**/Composition/**"]
          }
        }
        """);

        var configuration = new CoverageConfigurationLoader().Load(root);

        Assert.Multiple(() =>
        {
            Assert.That(configuration.Minimum, Is.EqualTo(90d));
            Assert.That(configuration.ReportDirectory, Is.EqualTo("artifacts/coverage"));
            Assert.That(configuration.Include, Is.EqualTo(new[] { "AgentUp.Server/**/*.cs" }));
            Assert.That(configuration.Exclude, Is.EqualTo(new[] { "**/Composition/**" }));
        });
    }

    [Test]
    public void Load_throwsWhenMinimumIsMissing()
    {
        var root = WriteRepository("""{ "coverage": { "include": ["**/*.cs"] } }""");

        Assert.That(() => new CoverageConfigurationLoader().Load(root),
            Throws.TypeOf<CoverageConfigurationException>().With.Message.Contains("minimum"));
    }

    [TestCase("-1")]
    [TestCase("101")]
    public void Load_throwsWhenMinimumIsOutsideZeroToOneHundred(string minimum)
    {
        var root = WriteRepository($$"""{ "coverage": { "minimum": {{minimum}}, "include": ["**/*.cs"] } }""");

        Assert.That(() => new CoverageConfigurationLoader().Load(root),
            Throws.TypeOf<CoverageConfigurationException>().With.Message.Contains("between 0 and 100"));
    }

    [Test]
    public void Load_throwsWhenMinimumIsNotANumber()
    {
        var root = WriteRepository("""{ "coverage": { "minimum": "ninety", "include": ["**/*.cs"] } }""");

        Assert.That(() => new CoverageConfigurationLoader().Load(root),
            Throws.TypeOf<CoverageConfigurationException>().With.Message.Contains("must be a number"));
    }

    [Test]
    public void Load_throwsWhenIncludeIsEmptyBecauseTheGateWouldMeasureNothing()
    {
        var root = WriteRepository("""{ "coverage": { "minimum": 90 } }""");

        Assert.That(() => new CoverageConfigurationLoader().Load(root),
            Throws.TypeOf<CoverageConfigurationException>().With.Message.Contains("include"));
    }

    [Test]
    public void Load_throwsOnMalformedJsonInsteadOfSilentlyDisablingTheGate()
    {
        var root = WriteRepository("""{ "coverage": { "minimum": """);

        Assert.That(() => new CoverageConfigurationLoader().Load(root),
            Throws.TypeOf<CoverageConfigurationException>());
    }

    [Test]
    public void Load_defaultsTheReportDirectoryWhenOmitted()
    {
        var root = WriteRepository("""{ "coverage": { "minimum": 90, "include": ["**/*.cs"] } }""");

        Assert.That(new CoverageConfigurationLoader().Load(root).ReportDirectory,
            Is.EqualTo("artifacts/coverage"));
    }
}
