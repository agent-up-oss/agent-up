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

    [Test]
    public void Load_readsTheSliceFloorAndItsExemptions()
    {
        var root = WriteRepository("""
        {
          "coverage": {
            "minimum": 90,
            "sliceMinimum": 70,
            "include": ["AgentUp.Server/**/*.cs"],
            "sliceExemptions": ["AgentUp.Server/Features/Ports"]
          }
        }
        """);

        var configuration = new CoverageConfigurationLoader().Load(root);

        Assert.Multiple(() =>
        {
            Assert.That(configuration.SliceMinimum, Is.EqualTo(70d));
            Assert.That(configuration.SliceExemptions,
                Is.EqualTo(new[] { "AgentUp.Server/Features/Ports" }));
        });
    }

    [Test]
    public void Load_treatsAnAbsentSliceFloorAsSwitchedOff()
    {
        // A repository with no slice layout has no slices to hold to a floor, so the
        // optional setting reads as zero rather than failing the load.
        var root = WriteRepository("""
        { "coverage": { "minimum": 90, "include": ["AgentUp.Server/**/*.cs"] } }
        """);

        var configuration = new CoverageConfigurationLoader().Load(root);

        Assert.Multiple(() =>
        {
            Assert.That(configuration.SliceMinimum, Is.EqualTo(0d));
            Assert.That(configuration.SliceExemptions, Is.Empty);
        });
    }

    [TestCase("-1")]
    [TestCase("101")]
    public void Load_throwsWhenTheSliceFloorIsOutsideZeroToOneHundred(string sliceMinimum)
    {
        var root = WriteRepository($$"""
        { "coverage": { "minimum": 90, "sliceMinimum": {{sliceMinimum}}, "include": ["a/**"] } }
        """);

        Assert.That(() => new CoverageConfigurationLoader().Load(root),
            Throws.InstanceOf<CoverageConfigurationException>());
    }

    [Test]
    public void Load_throwsWhenTheSliceFloorIsNotANumber()
    {
        var root = WriteRepository("""
        { "coverage": { "minimum": 90, "sliceMinimum": "seventy", "include": ["a/**"] } }
        """);

        Assert.That(() => new CoverageConfigurationLoader().Load(root),
            Throws.InstanceOf<CoverageConfigurationException>());
    }

    [Test]
    public void Load_throwsWhenSliceExemptionsIsNotAnArray()
    {
        var root = WriteRepository("""
        { "coverage": { "minimum": 90, "include": ["a/**"], "sliceExemptions": "everything" } }
        """);

        Assert.That(() => new CoverageConfigurationLoader().Load(root),
            Throws.InstanceOf<CoverageConfigurationException>());
    }

    [TestCase("AgentUp.Server")]
    [TestCase("AgentUp.Server/Features")]
    [TestCase("AgentUp.Server/Features/Ports/Services")]
    [TestCase("AgentUp.Server/Shared/Providers")]
    [TestCase("AgentUp.Server/Features/**")]
    public void Load_throwsWhenAnExemptionIsNotExactlyOneSlicePath(string entry)
    {
        // A glob or a type folder would silently exempt slices nobody reviewed, and could
        // never match the exact slice paths the check reports.
        var root = WriteRepository($$"""
        { "coverage": { "minimum": 90, "include": ["a/**"], "sliceExemptions": ["{{entry}}"] } }
        """);

        Assert.That(() => new CoverageConfigurationLoader().Load(root),
            Throws.InstanceOf<CoverageConfigurationException>()
                .With.Message.Contains(entry));
    }

    [Test]
    public void Load_throwsWhenAnExemptionIsNotAString()
    {
        var root = WriteRepository("""
        { "coverage": { "minimum": 90, "include": ["a/**"], "sliceExemptions": [70] } }
        """);

        Assert.That(() => new CoverageConfigurationLoader().Load(root),
            Throws.InstanceOf<CoverageConfigurationException>());
    }
}
